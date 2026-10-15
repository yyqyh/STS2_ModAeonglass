using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;


namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 随机数的<b>调试探针</b>：打印某条流的接下来 N 个值（只读，给控制面板/日志用）。
/// </summary>
/// <remarks>
/// ① 职责：<c>ProbeRunRng</c> / <c>ProbeModRng</c> / <c>ProbeMonsterRng</c>。
/// ② 调用时机：调试与控制面板（不参与机制结算）。
/// ③ 依赖：<c>RngOps</c> 的快照读取。
/// ④ ⚠️ 只读打印：绝不推进或改写任何随机流。
/// </remarks>
internal static class RngProbes
{

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
}
