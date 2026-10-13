using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding;

/// <summary>
/// M4 的 RNG 快照（本 Lab 补齐的唯一规格缺口）。
/// 覆盖三处：
/// <list type="number">
///   <item><c>RunRngSet</c>（含全部 <c>RunRngType</c> 子流）—— <c>ToSerializable()</c> / <c>LoadFromSerializable()</c>；</item>
///   <item>每只怪的 <c>MonsterModel.Rng</c>（<c>CombatState.CreateCreature</c> 按 mapCoord+act+combatId 播种的那个）；</item>
///   <item>mod 自己的 <c>StsRng</c>（<c>ModRunRngRegistry</c> / <c>RitsuLibFramework.GetModRunRng</c>）。</item>
/// </list>
/// <b>一律"灌回原实例"</b>（<c>LoadFromSerializable</c>），不替换实例 —— 引擎里到处按引用持有这些 Rng。
/// </summary>
internal sealed class RngSnapshot
{
    internal SerializableRunRngSet? RunRng { get; init; }

    /// <summary>按生物引用索引（<see cref="ReferenceEqualityComparer"/>：不能用值比较）。</summary>
    internal Dictionary<Creature, SerializableRng> MonsterRng { get; } =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>mod 自己的 StsRng，按 <c>ModRunRngRegistry</c> 的 streamId 索引。</summary>
    internal Dictionary<string, SerializableRng> ModRng { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    internal int EntryCount => (RunRng is null ? 0 : 1) + MonsterRng.Count + ModRng.Count;
}

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

    /// <summary>
    /// 自检探针：从 run Rng 的 <c>CombatTargets</c> 子流取 <paramref name="count"/> 个值。
    /// "回档后重放同一手牌 ⇒ 随机结果一致"的最直观判据就是"两次取出来的序列相同"。
    /// </summary>
    internal static string ProbeRunRng(int count, RunRngType type = RunRngType.CombatTargets)
    {
        var state = CombatAccess.State;
        if (state is null)
        {
            return "no combat";
        }

        var rng = state.RunState.Rng.GetRng(type);
        var values = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(rng.NextInt(0, 1000));
        }

        return $"{type} counter={rng.ToSerializable().counter} seq=[{string.Join(",", values)}]";
    }

    /// <summary>自检探针：从 mod 自己的 StsRng 取值（验证 ModRunRngRegistry 的流也被灌回）。</summary>
    internal static string ProbeModRng(string stream, int count)
    {
        var state = CombatAccess.State;
        if (state?.RunState is not RunState runState)
        {
            return "no run";
        }

        var rng = RitsuLibFramework.GetModRunRng(runState, Const.ModId, stream);
        var values = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(rng.NextInt(0, 1000));
        }

        return $"{stream} counter={rng.ToSerializable().counter} seq=[{string.Join(",", values)}]";
    }

    /// <summary>自检探针：从某只怪的 <c>MonsterModel.Rng</c> 取值。</summary>
    internal static string ProbeMonsterRng(Creature? creature, int count)
    {
        if (creature?.Monster is not { } monster || !monster.IsMutable)
        {
            return "no mutable monster";
        }

        var rng = monster.Rng;
        var values = new List<int>(count);
        for (var i = 0; i < count; i++)
        {
            values.Add(rng.NextInt(0, 1000));
        }

        return $"{monster.Id.Entry}#{creature.CombatId?.ToString() ?? "-"} "
            + $"counter={rng.ToSerializable().counter} seq=[{string.Join(",", values)}]";
    }

    private static int RngCounter(SerializableRunRngSet set) =>
        set.Rngs.TryGetValue(RunRngType.CombatTargets, out var rng)
            ? rng.counter
            : -1;
}
