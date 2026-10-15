using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Rewind.Graph;
using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 战斗快照的<b>一侧（我方/敌方）</b>抓取助手：找出该侧生物、恢复战斗历史计数。
/// </summary>
/// <remarks>
/// ① 职责：<c>CreaturesOnSide</c>（按 side 取生物）+ <c>RestoreHistory</c>（把历史计数写回）。
/// ② 调用时机：<c>CombatStateSnapshot.Capture/TryRestore</c> 内部。
/// ③ 依赖：<c>CombatState</c> + 历史层。
/// ④ ⚠️ 只读/只写模型层，不碰 UI。
/// </remarks>
internal sealed partial class CombatStateSnapshot
{

    internal static List<Creature> CreaturesOnSide(CombatState state, CombatSide side) =>
        state.Creatures.Where(c => c.Side == side).ToList();

    private void RestoreHistory(IRewindLog log)
    {
        var history = CombatManager.Instance.History;
        if (!Reflect.TryGet<List<CombatHistoryEntry>>(history, Coordinates.History.Entries, out var list))
        {
            log.Warn("history entries field not found, skip history restore");
            return;
        }

        list.Clear();
        list.AddRange(_historyEntries);

        if (Reflect.TryGet<Action>(history, Coordinates.History.Changed, out var changed))
        {
            changed?.Invoke();
        }
    }

    internal void AddModel(string kind, AbstractModel model) =>
        _models.Add(new Entry(kind, model, FieldGraphSnapshot.Capture(model, MegaCritGraphPolicy.Instance)));

    public void Release()
    {
        _models.Clear();
        _piles.Clear();
        _historyEntries.Clear();
        _allies.Clear();
        _enemies.Clear();
        _secondaryResources.Clear();
        _rng = null;
        _turnFlags = null;
    }

    internal sealed record Entry(string Kind, AbstractModel Model, FieldGraphSnapshot Graph);

    public bool TryRestore(IRewindLog log, out string error)
    {
        // 恢复期标志：拦掉 PowerModel.SetAmount 的 DisplayAmountChanged / InvokePowerModified（规格 §2.6.3 坑 1）。
        using var _guard = RestoreGuard.Enter();

        // 第 3 步（必须最先）：恢复 RNG —— 之后的任何恢复动作都可能消耗 RNG，先灌回去才能保证"重打同一手牌结果一致"。
        if (_scope.RestoreRng && !RngOps.Restore(_rng, log, out var rngError))
        {
            error = rngError;
            return false;
        }

        // 规格 §2.6.3 第 4 步：二级资源（沙砾 / 空隙）与能量 / 星星。
        if (_scope.RestoreSecondaryResources)
        {
            var bridge = SecondaryResourceRuntime.Bridge;
            if (bridge.Available && CombatAccess.LocalPlayer is { } owner)
            {
                bridge.Restore(owner, _secondaryResources);
                log.Info($"secondary resources restored ({_secondaryResources.Count} entr(ies))");
            }
            else if (!bridge.Available)
            {
                log.Info("secondary resources: bridge not wired, skipped");
            }
        }

        var player = CombatAccess.LocalPlayer;
        var state = CombatAccess.State;
        if (player is null || state is null || player.PlayerCombatState is not { } combat)
        {
            error = "no local player / no combat state";
            return false;
        }

        // 1) power 增删对齐：先移除快照后新增的，再补回快照里被移除的（顺序不可换）
        var snapshotPowers = _models
            .Where(e => e.Kind == PowerKind)
            .Select(e => (PowerModel)e.Model)
            .ToHashSet();
        var powersBefore = CombatAccess.DescribePowers();
        var alignment = PowerOps.Align(player.Creature, snapshotPowers, player.Creature.Powers);

        // 2) 模型字段：静默直写（不经过 setter，不发事件）
        var failures = new List<string>();
        var skipped = 0;
        var relics = player.Relics.ToHashSet();

        foreach (var entry in _models)
        {
            var stillThere = entry.Kind == PowerKind
                ? player.Creature.Powers.Contains((PowerModel)entry.Model)
                : relics.Contains((RelicModel)entry.Model);

            // 快照后被移除的模型暂不补回（后续里程碑）。
            if (!stillThere)
            {
                skipped++;
                continue;
            }

            if (!entry.Graph.TryRestore(entry.Model, MegaCritGraphPolicy.Instance, out var reason))
            {
                failures.Add($"{entry.Kind} {entry.Model.Id.Entry}: {reason}");
            }
        }

        if (failures.Count > 0)
        {
            error = string.Join("; ", failures);
            return false;
        }

        // 3) 牌堆：静默搬运（清空 → 按快照顺序放回），快照后新增的卡脱离战斗
        var pileResult = default(PileRestoreResult);
        if (_scope.RestorePiles)
        {
            pileResult = PileOps.Restore(player, _piles);
        }

        // 4) 血量 / 格挡（走属性 setter，血条与格挡数字自动刷新）
        var vitalsBefore = CombatAccess.DescribeVitals();
        var vitalsApplied = CreatureVitals.Restore(_allies, log, _scope.RestorePlayerHp, _scope.RestorePlayerBlock);
        vitalsApplied += CreatureVitals.Restore(_enemies, log, _scope.RestoreEnemyHp, _scope.RestoreEnemyBlock);

        // 5) 能量 / 星星 / 回合号 / 轮次
        if (_scope.RestoreTurnCounters)
        {
            combat.Energy = _energy;
            combat.Stars = _stars; // 会补一条 StarsModified 历史，第 4 步截断时一并抹掉
            Reflect.Set(combat, Coordinates.PlayerCombatState.TurnNumber, _turnNumber);
            state.RoundNumber = _roundNumber;

            // 相位：打点与回档都只允许发生在 Play，所以这一句实际是幂等的；
            // 走属性 setter（值相同不发事件），不直接写私有字段，避免 UI 漏事件。
            combat.Phase = _phase;
        }

        // 6) 战斗历史截断到快照长度（并补发 Changed）
        if (_scope.RestoreCombatHistory)
        {
            RestoreHistory(log);
        }

        // 6.5) 回合级私有标记写回"快照时的值"（规格 §2.6.2 最后一行 + §2.6.3 第 9 步）。
        if (_scope.RestoreTurnCounters)
        {
            TurnFlagOps.Restore(_turnFlags, log);
        }

        // 7) 刷新：模型数字 → 手牌投影
        RewindUiRefresh.AfterRestore(_models.Select(e => e.Model));
        if (_scope.RestorePiles)
        {
            HandUiSync.Sync(player);
        }

        log.Info(
            $"restored {_models.Count - skipped} model(s) (power +{alignment.ReAdded}/-{alignment.Removed}), "
            + $"{pileResult.Restored} card(s) (+{pileResult.Dropped} dropped), skipped {skipped}");
        log.Info($"rng now: {RngOps.DescribeLive()}");
        log.Info($"powers: {powersBefore} → {CombatAccess.DescribePowers()}");
        log.Info($"vitals: {vitalsBefore} → {CombatAccess.DescribeVitals()} ({vitalsApplied} creature(s))");

        error = string.Empty;
        return true;
    }
}
