using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Wither;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>本回合的判定结果（对应规格 §2.8.2 的三条分支）。</summary>
internal sealed record Decision(string Kind, IntentCard? Card, string Reason);

/// <summary>每回合的意图流程：回合开始 → 结算 → 判定 → 写 <c>NextMove</c>（规格 §2.8.2 的可执行版）。</summary>
/// <remarks>
/// ① 职责：<c>PrepareTurn</c> / <c>SettlePending</c> / 判定原因上报与滞回；抽弃洗在 <c>IntentHandCycle</c>、判定与动作在 <c>IntentDecision</c> / <c>IntentMoves</c>。
/// ② 挂点与时机：解析落在"玩家回合开始"（原生掷招已被接管补丁跳过）；玩家的动作当场重排并立刻重设 <c>NextMove</c>，敌人的回合只负责执行。
/// ③ 依赖：<c>IntentDeck</c>（三堆状态）、<c>IntentOps</c>（唯一写口）、<c>WitherPool</c>（虚无）、<c>IntentRules</c>。
/// ④ ⚠️ 三堆循环的顺序即语义：先结算上一回合打出的牌（进弃牌堆）→ 按 <see cref="HandEndMode" /> 清手牌 →
/// 抽满 N 张（抽牌堆空则把弃牌堆洗回）→ 判定 → 写 <c>NextMove</c>。
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
        var cleared = IntentHandCycle.ClearHand(creature, state);
        var drawn = IntentHandCycle.DrawHand(creature, state);
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
    /// 标记只挡"洗回"（见 <see cref="IntentHandCycle.ReshuffleDiscardIntoDeck" />），不挡任何主动指定 —— 与玩家侧迟滞同口径。
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
