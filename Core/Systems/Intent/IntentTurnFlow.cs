using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Wither;
namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>本回合的判定结果（对应规格 §2.8.2 的三条分支）。</summary>
internal sealed record Decision(string Kind, IntentCard? Card, string Reason);

/// <summary>
/// 每回合解析算法（规格 §2.8.2 的可执行版，**按新的三堆模型改写**）+ 玩家的两个动词。
/// </summary>
/// <remarks>
/// <para>
/// 时序：解析落在"玩家回合开始"（原生掷招已被 C11 接管跳过）；玩家的"跳过 / 提前"当场重排并立刻重设
/// <c>NextMove</c>；敌人的回合只负责执行。
/// </para>
/// <para>
/// <b>三堆循环</b>：回合开始 → 结算上一回合打出的牌（进弃牌堆）→ 清手牌（按 <see cref="HandEndMode" />）→
/// 抽满 N 张（抽牌堆空则把弃牌堆洗回）→ 判定 → 写 <c>NextMove</c>。
/// </para>
/// </remarks>
internal static class IntentTurnFlow
{
    // ============================ 回合开始 ============================

    public static string PrepareTurn(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        if (state.IntentCount == 0)
        {
            return $"{state.MonsterId}｜skip: 意图牌堆为空（这只怪不托管，原生掷招继续）";
        }

        var settled = SettlePending(creature, state);
        var cleared = ClearHand(creature, state);
        var drawn = DrawHand(creature, state);
        var delayed = ApplyHysteresis(creature, state);
        var decision = IntentDecision.Decide(state);
        IntentDecision.Apply(creature, state, decision);

        var text = $"{state.MonsterId}｜{settled}｜{cleared}｜{drawn}｜判定={decision.Kind}({decision.Reason})"
                   + $"｜{delayed}｜{state.Counts()}";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }

    /// <summary>
    /// 「迟滞注入」（DLY-4 静域）：手牌刚抽好，问玩家身上实现了
    /// <see cref="IIntentHysteresisSource" /> 的能力要不要钉住第一张意图。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 标记只挡"洗回"（见 <see cref="ReshuffleDiscardIntoDeck" />），不挡任何主动指定 —— 与玩家侧迟滞同口径。
    /// </para>
    /// <para>
    /// 只标记 <see cref="IntentCard.Playable" /> 的牌：虚无是纯元数据（本来就不参与循环），
    /// 被吃掉的意图也没人会打出。标记不到就不消费额度。
    /// </para>
    /// </remarks>
    private static string ApplyHysteresis(Creature creature, EnemyIntentState state)
    {
        var sources = (MonsterAccess.Combat?.Players ?? [])
            .SelectMany(player => player.Creature.Powers.OfType<IIntentHysteresisSource>())
            .ToList();
        if (sources.Count == 0)
        {
            return "迟滞：无来源";
        }

        var marked = 0;
        foreach (var source in sources)
        {
            var pending = source.PendingIntentHysteresis;
            if (pending <= 0)
            {
                continue;
            }

            var used = MarkFirstPlayable(state, pending);
            if (used <= 0)
            {
                continue;
            }

            source.ConsumeIntentHysteresis(used);
            marked += used;
        }

        return marked > 0
            ? $"迟滞：钉住 {marked} 张意图（不参与洗回）"
            : "迟滞：没有可标记的意图";
    }

    /// <summary>把手牌里最前面的 <paramref name="count" /> 张"可以行动的"意图打上迟滞，返回实际打了几张。</summary>
    private static int MarkFirstPlayable(EnemyIntentState state, int count)
    {
        var marked = 0;
        foreach (var card in state.Hand)
        {
            if (marked >= count)
            {
                break;
            }

            if (!card.Playable || card.Stuck)
            {
                continue;
            }

            card.Stuck = true;
            marked++;
        }

        return marked;
    }

    /// <summary>结算上一回合：敌人回合真的开始过 ⇒ 我们设进去的那张已经被引擎执行了。</summary>
    private static string SettlePending(Creature creature, EnemyIntentState state)
    {
        if (!state.EnemyTurnSeen || state.Pending is not { } played)
        {
            state.EnemyTurnSeen = false;
            return "结算：无（敌人回合没开始过）";
        }

        state.Pending = null;
        state.EnemyTurnSeen = false;
        state.Hand.Remove(played);
        played.TimesPlayed++;

        if (played.Consume)
        {
            // 消耗 = 从**卡组池**里永久移除（以后抽不到了），并记账参与守恒。
            state.Deck.Remove(played);
            state.Consumed.Add(played.MoveId);
            return $"结算：打出 {played.MoveId} → 消耗（移出卡组）";
        }

        state.Discard.Add(played);
        return $"结算：打出 {played.MoveId} → 弃牌堆";
    }

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
            WitherPool.ApplyPenalty(creature, state, card, "怪物行动后结算");
            state.VoidsSpent++;
            removed++;
        }

        return $"清手牌：{discarded} 张进弃牌堆 / {returned} 张回抽牌堆（口径 {handEnd}），"
               + $"虚无行动后结算并消耗 {removed} 张";
    }

    /// <summary>按口径把一张牌放回抽牌堆（确定性回底 / 随机洗回）。</summary>
    private static void ReturnToDeck(Creature creature, EnemyIntentState state, IntentCard card)
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
                WitherPool.ApplyPenalty(creature, state, card, "抽到");
            }
        }

        return $"抽牌：{drawn} 张（虚无 {drawnVoids}，洗回 {reshuffles} 次，固定 {target} 张，口径 {mode}）";
    }

    /// <summary>把弃牌堆整体洗回抽牌堆（用怪物的 AI 随机流；拿不到 RNG 就保持原顺序）。</summary>
    private static void ReshuffleDiscardIntoDeck(Creature creature, EnemyIntentState state)
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

    // ============================ 判定（§2.8.2）============================

    // ======================= 玩家动词（用户 2026-10-12 定稿：三个动作）=======================
    //
    // 「可以行动的」= 非虚无 + 没被虚无吃掉（「条件」自 2026-10-14 起**不再是门槛**，只是插队优先权）。
    // 三个动作都作用在**手牌**上，做完立刻 ReResolve（重算判定 + 重写 NextMove），
    // 所以卡带上的高亮/意图当场就会变。

    // ---------- 指向模式的"指定某一张"版本（用户 2026-10-12：动作卡打出后由玩家点某张意图）----------




    internal static string Report(string text, Creature creature)
    {
        var full = $"{text}｜{ReResolve(creature)}";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {full}");
        return full;
    }

    /// <summary>
    /// 改手牌里<b>第一张普通意图</b>的三属性（构造"条件不满足 / 强制 / 消耗"这些剧本）。
    /// 跳过虚无 —— 虚无不携带 MoveState，给它挂三属性没有意义。
    /// </summary>
    public static string CycleFrontFlag(Creature creature, string which)
    {
        var state = IntentDeck.Ensure(creature);
        var card = state.Hand.FirstOrDefault(c => !c.IsVoid);
        if (card is null)
        {
            return "skip: 手牌里没有普通意图卡（全是虚无）";
        }

        switch (which)
        {
            case "condition":
                card.ConditionMet = !card.ConditionMet;
                return $"第一张 {card.Describe()}｜{ReResolve(creature)}";
            case "forced":
                card.Forced = !card.Forced;
                return $"第一张 {card.Describe()}｜{ReResolve(creature)}";
            case "consume":
                card.Consume = !card.Consume;
                return $"第一张 {card.Describe()}";
            default:
                return $"skip: 未知标记 {which}";
        }
    }

    /// <summary>重排（不抽牌、不清手牌）：玩家的每一次操作都立刻反映到 <c>NextMove</c> 与意图 UI 上。</summary>
    public static string ReResolve(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        var decision = IntentDecision.Decide(state);
        IntentDecision.Apply(creature, state, decision);
        var text = $"重排 判定={decision.Kind}｜{IntentUiProbe.Describe(creature)}";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }
}
