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
/// 意图 快照（我方战斗态）：模型字段（power / 遗物）+ 战斗牌堆 + 能量星星 + 回合号轮次 + 战斗历史。
/// <b>不回档</b>：血量、金币、遗物列表、药水、敌人、卡组（见 <see cref="RewindScope" />）。
/// </summary>
internal sealed class CombatStateSnapshot : ISnapshot
{
    private const string PowerKind = "power";

    private const string RelicKind = "relic";

    private readonly RewindScope _scope;
    private readonly List<Entry> _models = [];
    private readonly List<CombatHistoryEntry> _historyEntries = [];

    private Dictionary<PileType, List<CardModel>> _piles = [];
    private Dictionary<Creature, Vitals> _allies = [];
    private Dictionary<Creature, Vitals> _enemies = [];
    private Dictionary<string, int> _secondaryResources = [];
    private RngSnapshot? _rng;
    private TurnFlagOps.TurnFlags? _turnFlags;
    private int _energy;
    private int _stars;
    private int _turnNumber;
    private int _roundNumber;
    private PlayerTurnPhase _phase;

    private CombatStateSnapshot(SnapshotId id, string summary, RewindScope scope)
    {
        Id = id;
        Summary = summary;
        _scope = scope;
    }

    public SnapshotId Id { get; }

    public string Summary { get; }

    internal static CombatStateSnapshot? Capture(SnapshotId id, RewindScope scope)
    {
        var player = CombatAccess.LocalPlayer;
        var state = CombatAccess.State;
        if (player is null || state is null || player.PlayerCombatState is not { } combat)
        {
            return null;
        }

        var snapshot = new CombatStateSnapshot(id, CombatAccess.Describe(), scope);

        foreach (var power in player.Creature.Powers)
        {
            snapshot.AddModel(PowerKind, power);
        }

        foreach (var relic in player.Relics)
        {
            snapshot.AddModel(RelicKind, relic);
        }

        if (scope.RestorePiles)
        {
            snapshot._piles = PileOps.Capture(player);
        }

        if (scope.RestoreCombatHistory)
        {
            snapshot._historyEntries.AddRange(CombatManager.Instance.History.Entries);
        }

        if (scope.RestorePlayerHp || scope.RestorePlayerBlock)
        {
            snapshot._allies = CreatureVitals.Capture(CreaturesOnSide(state, CombatSide.Player));
        }

        if (scope.RestoreEnemyHp || scope.RestoreEnemyBlock)
        {
            snapshot._enemies = CreatureVitals.Capture(CreaturesOnSide(state, CombatSide.Enemy));
        }

        if (scope.RestoreSecondaryResources && SecondaryResourceRuntime.Bridge.Available)
        {
            snapshot._secondaryResources =
                new Dictionary<string, int>(SecondaryResourceRuntime.Bridge.Capture(player));
        }

        // 回溯 唯一补齐的规格缺口：RNG（run Rng 集合 + 每只怪的 Rng + mod 自己的 StsRng）。
        if (scope.RestoreRng)
        {
            snapshot._rng = RngOps.Capture();
        }

        if (Reflect.TryGet<int>(combat, Coordinates.PlayerCombatState.TurnNumber, out var turn))
        {
            snapshot._turnNumber = turn;
        }

        snapshot._energy = combat.Energy;
        snapshot._stars = combat.Stars;
        snapshot._roundNumber = state.RoundNumber;
        snapshot._phase = combat.Phase;

        // 回合级私有标记（怪的本回合标记 / 首动 / 招式已执行）—— 规格 §2.6.2 最后一行。
        if (scope.RestoreTurnCounters)
        {
            snapshot._turnFlags = TurnFlagOps.Capture();
        }

        return snapshot;
    }

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

    private static List<Creature> CreaturesOnSide(CombatState state, CombatSide side) =>
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

    private void AddModel(string kind, AbstractModel model) =>
        _models.Add(new Entry(kind, model, FieldGraphSnapshot.Capture(model, MegaCritGraphPolicy.Instance)));

    private sealed record Entry(string Kind, AbstractModel Model, FieldGraphSnapshot Graph);
}
