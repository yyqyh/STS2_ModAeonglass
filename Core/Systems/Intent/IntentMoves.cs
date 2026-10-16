using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Wither;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图的<b>动作</b>：推迟 / 提前 / 剥夺 / 重排 / 拆解 / 洗乱（卡牌与调试面板都走这里）。
/// </summary>
/// <remarks>
/// ① 职责：校验目标、改手牌顺序或移除某张，并写清"为什么被拒"的文案。
/// ② 调用时机：意图的动词卡（提前/威吓/思维剥夺）、「拆解」、「精神错乱」、控制面板验证。
/// ③ 依赖：<c>IntentDeck</c>（三堆）+ <c>IntentOps</c>（状态机写入口）。
/// ④ ⚠️ 候选只有"可行动的"意图（与表现层点选口径一致）；被移除的那张立刻进它的弃牌堆。
/// </remarks>
internal static partial class IntentMoves
{

    /// <summary>「推迟」：把**第一张"可以行动的"**意图移到手牌**最后**。</summary>
    public static string Postpone(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        var index = IntentDecision.FirstActionableIndex(state);
        if (index < 0)
        {
            return NoActionable("推迟");
        }

        var card = state.Hand[index];
        state.Hand.RemoveAt(index);
        state.Hand.Add(card);

        return IntentTurnFlow.Report($"推迟 {card.MoveId}：手牌第 {index} 张 → 最后", creature);
    }

    /// <summary>「提前」：把**最后一张"可以行动的"**意图移到手牌**最前**。</summary>
    public static string Advance(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        var index = IntentDecision.LastActionableIndex(state);
        if (index < 0)
        {
            return NoActionable("提前");
        }

        var card = state.Hand[index];
        state.Hand.RemoveAt(index);
        state.Hand.Insert(0, card);

        return IntentTurnFlow.Report($"提前 {card.MoveId}：手牌第 {index} 张 → 最前", creature);
    }

    /// <summary>「剥夺」：**丢弃第一张"可以行动的"**意图（进弃牌堆，本回合不再出现在手牌）。</summary>
    public static string Deprive(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        var index = IntentDecision.FirstActionableIndex(state);
        if (index < 0)
        {
            return NoActionable("剥夺");
        }

        var card = state.Hand[index];
        state.Hand.RemoveAt(index);
        state.Discard.Add(card);

        return IntentTurnFlow.Report($"剥夺 {card.MoveId}：手牌第 {index} 张 → 弃牌堆（弃牌堆 {state.Discard.Count} 张）", creature);
    }

    /// <summary>
    /// <b>「定序」</b>（INT-5）：把整手牌按玩家给的顺序重排（<b>全部</b>，含虚无 —— 它也要占位），
    /// 然后立刻重新解析（重设 <c>NextMove</c>，让"将要打出的那张"跟着新顺序走）。
    /// </summary>
    /// <remarks>
    /// 顺序必须与手牌**一一对应**（数量相同、元素都在手牌里）才生效；
    /// 不满足就整条拒绝（宁可不变，也不要半边重排）。
    /// </remarks>
    public static string Reorder(Creature creature, IReadOnlyList<IntentCard> order)
    {
        var state = IntentDeck.Ensure(creature);
        if (order.Count != state.Hand.Count || order.Any(card => !state.Hand.Contains(card)))
        {
            var reject = $"定序：顺序（{order.Count} 张）与手牌（{state.Hand.Count} 张）对不上 ⇒ 不生效";
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} {reject}");
            return reject;
        }

        var before = string.Join(",", state.Hand.Select(card => card.MoveId));
        state.Hand.Clear();
        state.Hand.AddRange(order);

        return IntentTurnFlow.Report(
            $"定序：[{before}] → [{string.Join(",", order.Select(card => card.MoveId))}]", creature);
    }

    /// <summary>
    /// <b>「拆解」</b>（INT-4）：把<b>抽牌堆里</b>指定的一张挑出来放进它的弃牌堆。
    /// </summary>
    /// <remarks>
    /// 只要求"这张还在抽牌堆里"（不强制必须是顶上第一张）—— 玩家的选择本来就来自"顶上前 3 张"的界面，
    /// 但界面对着的同时，回合/洗牌都可能把这张挪走 ⇒ 挪走了就拒绝，不硬改。
    /// </remarks>
    public static string Dismantle(Creature creature, IntentCard target)
    {
        var state = IntentDeck.Ensure(creature);
        if (!state.Deck.Contains(target))
        {
            var reject = $"拆解：{target.MoveId} 已经不在抽牌堆里 ⇒ 不生效";
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} {reject}");
            return reject;
        }

        state.Deck.Remove(target);
        state.Discard.Add(target);

        return IntentTurnFlow.Report(
            $"拆解：抽牌堆 {target.MoveId} → 弃牌堆"
            + $"（抽牌堆 {state.Deck.Count} 张，弃牌堆 {state.Discard.Count} 张）",
            creature);
    }

    /// <summary>
    /// <b>「精神错乱」</b>（NEW-2）：把这只怪的整手牌<b>洗乱</b>（顺序随机），然后重新解析。
    /// </summary>
    /// <remarks>
    /// 随机流用怪物自己的 AI 流（与洗回、剥夺取随机同一套）⇒ 跑局内可复现、回档也被快照覆盖。
    /// 洗的是**顺序**，不抽不弃：手牌数量、内容、三属性都不变 ⇒ 只是"它要打哪张"变得不可预测。
    /// </remarks>
    public static string Scramble(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        if (state.Hand.Count <= 1)
        {
            var few = $"精神错乱：{state.MonsterId} 手牌只有 {state.Hand.Count} 张 ⇒ 洗乱没有意义";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {few}");
            return few;
        }

        if (creature.Monster?.RunRng.MonsterAi is not { } rng)
        {
            var noRng = $"精神错乱：拿不到 {state.MonsterId} 的随机流 ⇒ 不洗（保持原顺序）";
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} {noRng}");
            return noRng;
        }

        // Fisher-Yates（与"弃牌堆洗回"同一套写法）。
        for (var i = state.Hand.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (state.Hand[i], state.Hand[j]) = (state.Hand[j], state.Hand[i]);
        }

        return IntentTurnFlow.Report($"精神错乱：手牌洗乱 → [{string.Join(",", state.Hand.Select(card => card.MoveId))}]", creature);
    }

    /// <summary>目标必须"在这只怪的手牌里 + 可以行动"；否则拒绝（不消耗、不动牌）。</summary>
    private static bool TryLocate(
        EnemyIntentState state, IntentCard target, string verb, out int index, bool allowWither = false)
    {
        index = state.Hand.IndexOf(target);
        if (index < 0)
        {
            return false;
        }

        return IntentDecision.IsActionable(target) || (allowWither && target.IsVoid);
    }

    private static string TargetRejected(string verb, IntentCard target)
    {
        var text = $"{verb}：目标 {target.MoveId} 不能选（不在手牌里 / 不可行动）";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }

    private static string NoActionable(string verb)
    {
        var text = $"{verb}：手牌里没有「可以行动的」意图（非虚无 + 没被虚无吃掉）";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }

    /// <summary>「推迟」指定的那一张（必须是手牌里、且可行动的）。</summary>
    public static string Postpone(Creature creature, IntentCard target)
    {
        var state = IntentDeck.Ensure(creature);
        if (!TryLocate(state, target, "推迟", out var index))
        {
            return TargetRejected("推迟", target);
        }

        state.Hand.RemoveAt(index);
        state.Hand.Add(target);
        return IntentTurnFlow.Report($"推迟 {target.MoveId}：手牌第 {index} 张 → 最后", creature);
    }

    /// <summary>「提前」指定的那一张。</summary>
    public static string Advance(Creature creature, IntentCard target)
    {
        var state = IntentDeck.Ensure(creature);
        if (!TryLocate(state, target, "提前", out var index))
        {
            return TargetRejected("提前", target);
        }

        state.Hand.RemoveAt(index);
        state.Hand.Insert(0, target);
        return IntentTurnFlow.Report($"提前 {target.MoveId}：手牌第 {index} 张 → 最前", creature);
    }

    /// <summary>「剥夺」指定的那一张（进弃牌堆）。</summary>
    public static string Deprive(Creature creature, IntentCard target)
    {
        var state = IntentDeck.Ensure(creature);
        // 用户 2026-10-12：**凋萎可以被「剥夺」点名**（提前清掉这次掉血）—— 推迟/提前仍然不行。
        if (!TryLocate(state, target, "剥夺", out var index, allowWither: true))
        {
            return TargetRejected("剥夺", target);
        }

        state.Hand.RemoveAt(index);
        state.Discard.Add(target);
        return IntentTurnFlow.Report($"剥夺 {target.MoveId}：手牌第 {index} 张 → 弃牌堆（弃牌堆 {state.Discard.Count} 张）", creature);
    }

}
