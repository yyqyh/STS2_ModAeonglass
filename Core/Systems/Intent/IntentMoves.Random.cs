using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图动作的<b>随机版</b>（「拆招」用）：与「剥夺」同候选口径，只把"第一张"换成"随机一张"。
/// </summary>
/// <remarks>
/// ① 职责：<c>DepriveRandom</c>（随机丢一张进它的弃牌堆）+ 攻击判定。
/// ② 调用时机：卡「拆招」（NEW-18）打出、经指向链路拿到目标之后。
/// ③ 依赖：<c>IntentDeck</c>（三堆）+ 怪物自己的 AI 随机流（可复现、被回溯快照覆盖）。
/// ④ ⚠️ 攻击判定只认 <c>IntentType.Attack</c>（2026-10-16 用户拍板：处决 DeathBlow 不算）。
/// </remarks>
internal static partial class IntentMoves
{
    /// <summary>
    /// <b>「拆招」</b>（NEW-18）：从"可行动的意图"里<b>随机</b>丢掉一张（进它的弃牌堆）。
    /// </summary>
    /// <param name="wasAttack">丢掉的那张是不是<b>攻击意图</b>（调用方据此决定要不要补抽）。</param>
    /// <remarks>
    /// <para>
    /// 候选与「剥夺」完全同口径（只从<b>可行动</b>的意图里挑，见 <c>IntentDecision.IsActionable</c>），
    /// 唯一区别是"随机一张"而不是"第一张"。
    /// </para>
    /// <para>
    /// 随机流用这只怪自己的 AI 流（与「精神错乱」/ 洗回同一套）⇒ 跑局内可复现、回档也被快照覆盖。
    /// </para>
    /// </remarks>
    public static string DepriveRandom(Creature creature, out bool wasAttack)
    {
        wasAttack = false;
        var state = IntentDeck.Ensure(creature);
        var candidates = state.Hand.Where(IntentDecision.IsActionable).ToList();
        if (candidates.Count == 0)
        {
            return NoActionable("拆招");
        }

        if (creature.Monster?.RunRng.MonsterAi is not { } rng)
        {
            var noRng = $"拆招：拿不到 {state.MonsterId} 的随机流 ⇒ 什么都不丢";
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} {noRng}");
            return noRng;
        }

        var card = candidates[rng.NextInt(candidates.Count)];
        wasAttack = IsAttackIntent(creature, card);

        state.Hand.Remove(card);
        state.Discard.Add(card);
        IntentOps.Touch(state);

        return IntentTurnFlow.Report(
            $"拆招 {card.MoveId}：手牌随机一张 → 弃牌堆（攻击={wasAttack}，弃牌堆 {state.Discard.Count} 张）",
            creature);
    }

    /// <summary>这张意图是不是攻击意图（只认 <c>IntentType.Attack</c>）。</summary>
    private static bool IsAttackIntent(Creature creature, IntentCard card) =>
        IntentCycle.Resolve(creature, card)?.Intents?.Any(intent => intent.IntentType == IntentType.Attack) ?? false;
}
