using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Wither;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图的<b>手牌循环</b>：清手牌、回抽牌堆、抽满手牌、弃牌堆洗回（怪物侧的三堆循环）。
/// </summary>
/// <remarks>
/// ① 职责：一手牌的进出与洗回，是"怪物像玩家一样循环"的那半边。
/// ② 调用时机：<c>IntentTurnFlow</c> 的回合流程每回合调一次。
/// ③ 依赖：<c>IntentDeck</c>（三堆状态）+ <c>IntentRules</c>（口径：回合末去向 / 虚无抽牌模式）。
/// ④ ⚠️ 回合末"未打出的意图去哪"读设置；洗回会把弃牌堆整堆搬回抽牌堆。
/// </remarks>
internal static class IntentHandCycle
{

    /// <summary>
    /// 回合结束清空手牌。
    /// </summary>
    /// <remarks>
    /// 默认把未打出的意图卡**一并送进弃牌堆**（这样"抽牌堆→手牌→弃牌堆→洗回"才闭环）；
    /// 设置里可退回 Lab C 的旧行为（回抽牌堆底 / 随机洗回）。
    /// <para>
    /// <b>虚无的结算点就在这里</b>（用户 2026-10-11 定稿：怪物行动之后）：本方法在**下一个玩家回合开始**执行，
    /// 也就是"怪物已经打完这一回合"之后 ⇒ 这一刻先<b>掉血惩戒</b>、再<b>消耗掉</b>那张虚无（移除、不进弃牌堆），
    /// 然后才抽牌。<c>recycle</c> 口径下虚无进弃牌堆（会被洗回来再惩戒）；<c>nopenalty</c> 不惩戒。
    /// </para>
    /// </remarks>
    public static string ClearHand(Creature creature, EnemyIntentState state)
    {
        if (state.Hand.Count == 0)
        {
            return "清手牌：本来就是空的";
        }

        var voidMode = IntentRules.VoidMode;
        var handEnd = IntentRules.HandEnd;
        var discarded = 0;
        var returned = 0;
        var removed = 0;
        var settledVoids = new List<IntentCard>();

        foreach (var card in state.Hand)
        {
            card.Eaten = false;

            if (card.IsVoid && voidMode == VoidDrawMode.RemoveAfterFirstDraw)
            {
                // 怪物行动之后：先惩戒，再消耗掉（离手即消失，与手牌里的 Ethereal 同义）。
                settledVoids.Add(card);
                continue;
            }

            if (handEnd == HandEndMode.ToDiscard)
            {
                state.Discard.Add(card);
                discarded++;
                continue;
            }

            ReturnToDeck(creature, state, card);
            returned++;
        }

        state.Hand.Clear();

        foreach (var card in settledVoids)
        {
            WitherEffects.ApplyPenalty(creature, state, card, "怪物行动后结算");
            state.VoidsSpent++;
            removed++;
        }

        return $"清手牌：{discarded} 张进弃牌堆 / {returned} 张回抽牌堆（口径 {handEnd}），"
               + $"虚无行动后结算并消耗 {removed} 张";
    }

    /// <summary>按口径把一张牌放回抽牌堆（确定性回底 / 随机洗回）。</summary>
    internal static void ReturnToDeck(Creature creature, EnemyIntentState state, IntentCard card)
    {
        if (IntentRules.HandEnd == HandEndMode.ToDeckShuffle && state.Deck.Count > 0
            && creature.Monster?.RunRng.MonsterAi is { } rng)
        {
            state.Deck.Insert(rng.NextInt(state.Deck.Count + 1), card);
            return;
        }

        state.Deck.Add(card);
    }

    /// <summary>
    /// 抽牌：**固定抽 <see cref="IntentDeck.DrawPerTurn" /> 张**（用户 2026-10-12 口径）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>运行阶段照搬玩家的"抽 / 弃 / 洗回"循环</b>（用户 2026-10-12 澄清）：
    /// 抽牌堆只能从顶上抽（顺序在构建时与每次洗回时已经随机过），抽空了就把弃牌堆整体洗回来接着抽。
    /// 所以每回合的 5 张是"这一轮洗牌里的下 5 张"，而不是"每次独立取样"。
    /// </para>
    /// <para>补足到 x 时复制出来的重复意图，在同一轮里会各出现一次（这正是"补到 x"的意义）。</para>
    /// </remarks>
    public static string DrawHand(Creature creature, EnemyIntentState state)
    {
        var mode = IntentRules.VoidMode;
        var target = IntentDeck.DrawPerTurn;
        var drawn = 0;
        var drawnVoids = 0;
        var reshuffles = 0;

        while (state.Hand.Count < target)
        {
            if (state.Deck.Count == 0)
            {
                if (state.Discard.Count == 0)
                {
                    break; // 真的没牌了（消耗 / 移除把牌掏空了）
                }

                ReshuffleDiscardIntoDeck(creature, state);
                reshuffles++;
            }

            var card = state.Deck[0];
            state.Deck.RemoveAt(0);
            card.TimesDrawn++;
            drawn++;
            state.Hand.Add(card);

            if (!card.IsVoid)
            {
                continue;
            }

            drawnVoids++;
            if (mode == VoidDrawMode.RecycleEachDraw)
            {
                WitherEffects.ApplyPenalty(creature, state, card, "抽到");
            }
        }

        return $"抽牌：{drawn} 张（虚无 {drawnVoids}，洗回 {reshuffles} 次，固定 {target} 张，口径 {mode}）";
    }

    /// <summary>把弃牌堆整体洗回抽牌堆（用怪物的 AI 随机流；拿不到 RNG 就保持原顺序）。</summary>
    internal static void ReshuffleDiscardIntoDeck(Creature creature, EnemyIntentState state)
    {
        // 迟滞（敌人侧）：带标记的意图不参与洗回，留在弃牌堆；
        // 兜底：整堆都是迟滞时忽略标记（否则抽牌堆永远是空的 = 死锁）。
        var stuck = state.Discard.Where(card => card.Stuck).ToList();
        if (stuck.Count == state.Discard.Count && stuck.Count > 0)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 迟滞兜底：要洗回的 {stuck.Count} 张全是迟滞 ⇒ 本次忽略");
            stuck.Clear();
        }

        var movable = stuck.Count == 0
            ? state.Discard.ToList()
            : state.Discard.Where(card => !card.Stuck).ToList();

        state.Deck.AddRange(movable);
        foreach (var card in movable)
        {
            state.Discard.Remove(card);
        }

        if (stuck.Count > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 迟滞：{stuck.Count} 张意图不参与洗回（留在弃牌堆）");
        }

        state.Reshuffles++;

        if (creature.Monster?.RunRng.MonsterAi is not { } rng)
        {
            return;
        }

        for (var i = state.Deck.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (state.Deck[i], state.Deck[j]) = (state.Deck[j], state.Deck[i]);
        }
    }
}
