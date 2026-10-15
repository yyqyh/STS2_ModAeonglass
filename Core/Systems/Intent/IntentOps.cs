using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图状态机的<b>唯一写入口</b>：所有本体招式改动都经它。
/// </summary>
/// <remarks>
/// ① 职责：设置下一步 / 推进 / 兜底 follow-up。
/// ② 调用时机：解析器与动作。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class IntentOps
{
    /// <summary>
    /// 告诉状态"它变过了"（表现层靠 <see cref="EnemyIntentState.Revision" /> 决定要不要重画）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 粗粒度口径：**一个 public 写方法末尾调一次**，不是逐行改。它只自增一个整数，
    /// 不碰守恒 / 判定 / 随机任何口径；多调一次只是让版本多走一格，无副作用。
    /// </para>
    /// <para>
    /// 注意本类自己的几个方法改的是<b>本体的招式状态机</b>（<c>SetMoveImmediate</c>），不直接动
    /// <see cref="EnemyIntentState" /> —— 那种情况由调用方（<c>IntentDecision.Apply</c>）负责 Touch。
    /// </para>
    /// </remarks>
    public static void Touch(EnemyIntentState? state) => state?.BumpRevision();

    /// <summary>
    /// 改写当前意图（C1/C2 的核心）。
    /// </summary>
    /// <remarks>
    /// <c>forceTransition</c> 必须为 true：false 时若 <c>NextMove.CanTransitionAway</c> 为假，
    /// 本体是<b>静默失败</b>（NextMove 不变、UI 不刷、无日志、无异常），我们连"失败了"都发现不了。
    /// </remarks>
    public static string Rewrite(Creature creature, MoveState state, bool force)
    {
        var monster = creature.Monster;
        if (monster is null)
        {
            return "skip: 不是怪物";
        }

        var beforeId = monster.NextMove.Id;
        monster.SetMoveImmediate(state, force);
        var afterId = monster.NextMove.Id;
        var changed = afterId == state.Id;

        var text = $"改意图(force={force}) {beforeId} → {afterId}（目标 {state.Id}，成功={changed}）";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }

    /// <summary>按 Id 改写；找不到就不动。</summary>
    public static string RewriteTo(Creature creature, string moveId, bool force)
    {
        var machine = MonsterAccess.MachineOf(creature);
        if (machine is null || !machine.States.TryGetValue(moveId, out var raw) || raw is not MoveState move)
        {
            return $"skip: 状态 {moveId} 不存在或不是 MoveState";
        }

        return Rewrite(creature, move, force);
    }

    /// <summary>
    /// 晕眩的推荐实现：先把"必须执行一次"的门关掉（覆盖 = 推迟，本来就要关门），
    /// 再走原生 <c>CreatureCmd.Stun</c> —— 这样原生 VFX（<c>NStunnedVfx</c>）、STUNNED 图标、
    /// follow-up 全都保住。原生 Stun 会被 <c>MustPerformOnceBeforeTransitioning</c> 静默挡住，所以必须先后者。
    /// </summary>
    public static string StunForced(Creature creature)
    {
        var monster = creature.Monster;
        if (monster is null)
        {
            return "skip: 不是怪物";
        }

        var before = monster.NextMove.Id;
        monster.NextMove.MustPerformOnceBeforeTransitioning = false;
        _ = CreatureCmd.Stun(creature, FallbackNextMoveId(creature));
        var after = monster.NextMove.Id;
        var followUp = (monster.NextMove as MoveState)?.FollowUpStateId ?? "(none)";
        var text = $"晕眩(forced) {before} → {after} followUp={followUp} 成功={after == Const.Intent.StunnedStateId}"
                   + $"｜isStunned={creature.IsStunned}";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }

    /// <summary>
    /// 晕眩的 follow-up 必须是<b>已注册</b>的状态 Id。
    /// </summary>
    /// <remarks>
    /// 本体 <c>StunInternal</c> 的 <c>nextMoveId</c> 缺省取 <c>StateLog.Last()</c>，
    /// <b>StateLog 为空会抛</b>（首回合 / 刚召唤就可能撞上）⇒ 我们一律显式传兜底 Id。
    /// </remarks>
    public static string FallbackNextMoveId(Creature creature)
    {
        var machine = MonsterAccess.MachineOf(creature);
        return machine?.States.Values.OfType<MoveState>()
                   .Where(m => m.Id != Const.Intent.UnsetMoveId && m.Id != Const.Intent.StunnedStateId)
                   .Select(m => m.Id)
                   .OrderBy(x => x, StringComparer.Ordinal)
                   .FirstOrDefault()
               ?? Const.Intent.UnsetMoveId;
    }
}
