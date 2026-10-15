using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 回合级私有标记的快照 / 重置（回档作用域表）。
/// </summary>
/// <remarks>
/// ① 职责：抓取 / 重置回合标记。
/// ② 调用时机：回档抓取/恢复。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
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
