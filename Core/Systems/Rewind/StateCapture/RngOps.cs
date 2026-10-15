using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 随机数的<b>快照与恢复</b>：本体各条随机流 + 本 mod 自己的流。
/// </summary>
/// <remarks>
/// ① 职责：<c>Capture</c> / <c>Restore</c> / <c>Describe</c> / <c>Probe*</c>（探针）。
/// ② 调用时机：回档抓取/恢复与调试。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class RngOps
{
    /// <summary>拍一份 RNG 快照。三处都取；取不到的（如怪不可变）跳过并记日志。</summary>
    internal static RngSnapshot? Capture(IRewindLog? log = null)
    {
        var state = CombatAccess.State;
        if (state is null)
        {
            log?.Warn("rng capture skipped: no combat state");
            return null;
        }

        var snapshot = new RngSnapshot { RunRng = state.RunState.Rng.ToSerializable() };

        var skipped = 0;
        foreach (var creature in state.Creatures)
        {
            if (creature.Monster is not { } monster)
            {
                continue;
            }

            if (!monster.IsMutable)
            {
                // 不可变的怪拿到的 Rng 是共享的 Rng.Chaotic —— 绝不能灌它。
                skipped++;
                continue;
            }

            snapshot.MonsterRng[creature] = monster.Rng.ToSerializable();
        }

        if (state.RunState is RunState runState)
        {
            foreach (var stream in Const.Rewind.ModRngStreams)
            {
                snapshot.ModRng[stream] =
                    RitsuLibFramework.GetModRunRng(runState, Const.ModId, stream).ToSerializable();
            }
        }

        log?.Info(
            $"rng capture: runRng=1 monsterRng={snapshot.MonsterRng.Count}(skipped {skipped}) "
            + $"modRng={snapshot.ModRng.Count} [{Describe(snapshot)}]");
        return snapshot;
    }

    /// <summary>
    /// 恢复 RNG。<b>必须在任何消耗 RNG 的动作之前调用</b>（规格 §2.6.3 第 3 步）。
    /// 全部灌回原实例；怪若已不在战斗 / 已不可变，则跳过该只并记日志。
    /// </summary>
    internal static bool Restore(RngSnapshot? snapshot, IRewindLog log, out string error)
    {
        error = string.Empty;
        if (snapshot is null)
        {
            return true;
        }

        var state = CombatAccess.State;
        if (state is null)
        {
            error = "no combat state";
            return false;
        }

        try
        {
            if (snapshot.RunRng is { } runRng)
            {
                state.RunState.Rng.LoadFromSerializable(runRng);
            }
        }
        catch (Exception ex)
        {
            error = $"runRng restore failed: {ex.GetType().Name}: {ex.Message}";
            return false;
        }

        var aliveMonsters = state.Creatures
            .Where(c => c.Monster is not null)
            .ToHashSet<Creature>(ReferenceEqualityComparer.Instance);
        var monsterRestored = 0;
        var monsterSkipped = 0;

        foreach (var (creature, rng) in snapshot.MonsterRng)
        {
            if (creature.Monster is not { } monster || !aliveMonsters.Contains(creature) || !monster.IsMutable)
            {
                monsterSkipped++;
                continue;
            }

            try
            {
                monster.Rng.LoadFromSerializable(rng);
                monsterRestored++;
            }
            catch (Exception ex)
            {
                monsterSkipped++;
                log.Warn($"monster rng restore failed ({monster.Id.Entry}): {ex.GetType().Name}");
            }
        }

        var modRestored = 0;
        if (state.RunState is RunState runState)
        {
            foreach (var (stream, rng) in snapshot.ModRng)
            {
                try
                {
                    RitsuLibFramework.GetModRunRng(runState, Const.ModId, stream).LoadFromSerializable(rng);
                    modRestored++;
                }
                catch (Exception ex)
                {
                    log.Warn($"mod rng restore failed ({stream}): {ex.GetType().Name}");
                }
            }
        }

        log.Info(
            $"rng restore: runRng=1 monsterRng={monsterRestored}(skipped {monsterSkipped}) "
            + $"modRng={modRestored} [{Describe(snapshot)}]");
        return true;
    }

    /// <summary>一行指纹：run Rng 的 counter + 每只怪 + mod 流（面板与日志都要能肉眼比对）。</summary>
    internal static string Describe(RngSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "no rng snapshot";
        }

        var parts = new List<string>();
        if (snapshot.RunRng is { } runRng)
        {
            parts.Add($"run[counter {RngCounter(runRng)}]");
        }

        foreach (var (creature, rng) in snapshot.MonsterRng)
        {
            parts.Add($"{creature.Monster?.Id.Entry ?? "?"}#{creature.CombatId?.ToString() ?? "-"}[counter {rng.counter}]");
        }

        foreach (var (stream, rng) in snapshot.ModRng)
        {
            parts.Add($"{stream}[counter {rng.counter}]");
        }

        return parts.Count == 0 ? "empty" : string.Join(" ", parts);
    }

    /// <summary>实时读数（不进快照）：当前 run Rng 的 counter 摘要，用于"回档前后对比"。</summary>
    internal static string DescribeLive()
    {
        var state = CombatAccess.State;
        if (state is null)
        {
            return "no combat";
        }

        var run = state.RunState.Rng.ToSerializable();
        var monsters = state.Creatures
            .Where(c => c.Monster is { IsMutable: true })
            .Select(c => $"{c.Monster!.Id.Entry}#{c.CombatId?.ToString() ?? "-"}[{c.Monster.Rng.ToSerializable().counter}]");
        var mod = Const.Rewind.ModRngStreams
            .Select(s => $"{s}[{(state.RunState is RunState rs ? RitsuLibFramework.GetModRunRng(rs, Const.ModId, s).ToSerializable().counter : -1)}]");
        return $"run[{RngCounter(run)}] {string.Join(" ", monsters)} {string.Join(" ", mod)}";
    }

    private static int RngCounter(SerializableRunRngSet set) =>
        set.Rngs.TryGetValue(RunRngType.CombatTargets, out var rng)
            ? rng.counter
            : -1;
}
