using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// <b>回合级私有标记</b>的快照 / 重置（规格 §2.6.2「回档作用域表」的最后一行）。
/// </summary>
/// <remarks>
/// <para>
/// 这些字段**不在** <c>FieldGraphSnapshot</c> 的覆盖范围里（那一层只走"我方 power + 遗物"），
/// 但它们真的会改变回档后的行为，所以必须单独收：
/// </para>
/// <list type="bullet">
///   <item><description><c>MonsterModel._spawnedThisTurn</c> —— 本回合新召唤的怪本回合不该行动；</description></item>
///   <item><description><c>MonsterMoveStateMachine._performedFirstMove</c> —— 决定第一次 <c>RollMove</c> 是否原地返回；</description></item>
///   <item><description><c>MoveState._performedAtLeastOnce</c> —— <c>MustPerformOnceBeforeTransitioning</c> 的"必须打一次才准换招"。</description></item>
/// </list>
/// <para>
/// <b>写回的是"快照时的值"，不是一律 false</b>（规格 V6）——
/// 一律置 false 会让"本回合新召唤、本不该行动"的怪在回档后行动，属于引入新 bug。
/// </para>
/// <para>
/// 遗物 / 附魔的"每回合一次"标记（<c>_usedThisCombat</c> 那一类）**已经**由模型字段图覆盖
/// （它们在 <c>CombatStateSnapshot</c> 里作为 <c>RelicKind</c> 条目被逐字段快照），所以这里不再重复处理。
/// </para>
/// </remarks>
internal static class TurnFlagOps
{
    /// <summary>一份回合级标记快照（按实例引用索引）。</summary>
    internal sealed class TurnFlags
    {
        internal Dictionary<MonsterModel, bool> Spawned { get; } =
            new(ReferenceEqualityComparer.Instance);

        internal Dictionary<MonsterMoveStateMachine, bool> FirstMove { get; } =
            new(ReferenceEqualityComparer.Instance);

        internal Dictionary<MoveState, bool> Performed { get; } =
            new(ReferenceEqualityComparer.Instance);

        internal int EntryCount => Spawned.Count + FirstMove.Count + Performed.Count;

        /// <summary>一行摘要（日志用）。</summary>
        internal string Describe() =>
            $"spawned {Spawned.Count(v => v.Value)}/{Spawned.Count} "
            + $"firstMove {FirstMove.Count(v => v.Value)}/{FirstMove.Count} "
            + $"performed {Performed.Count(v => v.Value)}/{Performed.Count}";
    }

    /// <summary>拍一份回合级标记快照。</summary>
    internal static TurnFlags Capture()
    {
        var flags = new TurnFlags();
        if (CombatAccess.State is not { } state)
        {
            return flags;
        }

        foreach (var creature in state.Creatures)
        {
            if (creature.Monster is not { } monster)
            {
                continue;
            }

            if (Reflect.TryGet<bool>(monster, Coordinates.MonsterModel.SpawnedThisTurn, out var spawned))
            {
                flags.Spawned[monster] = spawned;
            }

            if (monster.MoveStateMachine is not { } machine)
            {
                continue;
            }

            if (Reflect.TryGet<bool>(machine, Coordinates.MonsterMoveStateMachine.PerformedFirstMove, out var first))
            {
                flags.FirstMove[machine] = first;
            }

            foreach (var move in machine.States.Values.OfType<MoveState>())
            {
                if (Reflect.TryGet<bool>(move, Coordinates.MoveState.PerformedAtLeastOnce, out var performed))
                {
                    flags.Performed[move] = performed;
                }
            }
        }

        return flags;
    }

    /// <summary>把回合级标记写回"快照时的值"。返回写成功的条数。</summary>
    internal static int Restore(TurnFlags? flags, IRewindLog log)
    {
        if (flags is null)
        {
            return 0;
        }

        var restored = 0;
        var failed = 0;

        foreach (var (monster, value) in flags.Spawned)
        {
            if (Reflect.Set(monster, Coordinates.MonsterModel.SpawnedThisTurn, value))
            {
                restored++;
            }
            else
            {
                failed++;
            }
        }

        foreach (var (machine, value) in flags.FirstMove)
        {
            if (Reflect.Set(machine, Coordinates.MonsterMoveStateMachine.PerformedFirstMove, value))
            {
                restored++;
            }
            else
            {
                failed++;
            }
        }

        foreach (var (move, value) in flags.Performed)
        {
            if (Reflect.Set(move, Coordinates.MoveState.PerformedAtLeastOnce, value))
            {
                restored++;
            }
            else
            {
                failed++;
            }
        }

        log.Info($"turn flags restored: {restored} field(s)（{flags.Describe()}）"
                 + (failed > 0 ? $"，失败 {failed}" : string.Empty));
        return restored;
    }
}
