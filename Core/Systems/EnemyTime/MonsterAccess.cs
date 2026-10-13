using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2_Aeonglass.Core.Systems.Rewind.Binding;

namespace STS2_Aeonglass.Core.Systems.EnemyTime;

/// <summary>
/// 本体访问的唯一入口（读多写少）：取战斗 / 敌人 / 状态机 / 节点。
/// 写操作一律走 <see cref="IntentOps" />。
/// </summary>
internal static class MonsterAccess
{
    private static int _focusIndex;

    public static CombatState? Combat => CombatManager.Instance.DebugOnlyGetState();

    public static bool InCombat => CombatManager.Instance.IsInProgress;

    public static IReadOnlyList<Creature> Enemies => Combat?.Enemies ?? [];

    public static IReadOnlyList<Creature> LivingEnemies => Enemies.Where(c => !c.IsDead).ToList();

    /// <summary>调试面板的默认操作对象：活着的敌人里按序号轮换（多怪战斗用「切换目标」）。</summary>
    public static Creature? Focus
    {
        get
        {
            var living = LivingEnemies;
            if (living.Count == 0)
            {
                return null;
            }

            return living[((_focusIndex % living.Count) + living.Count) % living.Count];
        }
    }

    public static Creature? CycleFocus()
    {
        _focusIndex++;
        return Focus;
    }

    public static MonsterModel? MonsterOf(Creature? creature) => creature?.Monster;

    public static MonsterMoveStateMachine? MachineOf(Creature? creature) => creature?.Monster?.MoveStateMachine;

    public static MoveState? NextMoveOf(Creature? creature) => creature?.Monster?.NextMove;

    public static NCreature? NodeOf(Creature? creature) =>
        creature is null ? null : NCombatRoom.Instance?.GetCreatureNode(creature);

    /// <summary>能不能托管这只怪（有怪物模型 + 在敌人侧 + 有状态机）。</summary>
    public static bool IsManageable(Creature? creature) =>
        creature is { IsDead: false } &&
        creature.Side == CombatSide.Enemy &&
        creature.Monster?.MoveStateMachine is not null;

    public static string IdOf(Creature? creature) => creature?.Monster?.Id.Entry ?? "-";

    /// <summary>一行状态机摘要（日志 / 面板用；读的都是只读或公开信息）。</summary>
    public static string DescribeMachine(Creature? creature)
    {
        var machine = MachineOf(creature);
        if (machine is null)
        {
            return "no machine";
        }

        var current = FieldText(machine, Coordinates.MonsterMoveStateMachine.CurrentState);
        var performedFirst = FieldText(machine, Coordinates.MonsterMoveStateMachine.PerformedFirstMove);
        var next = NextMoveOf(creature)?.Id ?? "-";
        var moves = machine.States.Values.OfType<MoveState>()
            .Count(m => m.Id != Const.Intent.UnsetMoveId && m.Id != Const.Intent.StunnedStateId);
        return $"monster={IdOf(creature)} next={next} current={current} firstMoveDone={performedFirst} "
               + $"states={machine.States.Count} moves={moves} log={machine.StateLog.Count}";
    }

    /// <summary>把一个私有字段读成文本（读不到就 "?"）。</summary>
    public static string FieldText(object? owner, string name) =>
        owner is null
            ? "?"
            : Reflect.TryGet<object>(owner, name, out var value) ? value?.ToString() ?? "null" : "?";
}
