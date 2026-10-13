using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace STS2_Aeonglass.Core.Systems.EnemyTime;

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
internal static class IntentResolver
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
        var decision = Decide(state);
        Apply(creature, state, decision);

        var text = $"{state.MonsterId}｜{settled}｜{cleared}｜{drawn}｜判定={decision.Kind}({decision.Reason})"
                   + $"｜{state.Counts()}";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
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

        var voidMode = EnemyTimeScope.VoidMode;
        var handEnd = EnemyTimeScope.HandEnd;
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
            VoidPollution.ApplyPenalty(creature, state, card, "怪物行动后结算");
            state.VoidsSpent++;
            removed++;
        }

        return $"清手牌：{discarded} 张进弃牌堆 / {returned} 张回抽牌堆（口径 {handEnd}），"
               + $"虚无行动后结算并消耗 {removed} 张";
    }

    /// <summary>按口径把一张牌放回抽牌堆（确定性回底 / 随机洗回）。</summary>
    private static void ReturnToDeck(Creature creature, EnemyIntentState state, IntentCard card)
    {
        if (EnemyTimeScope.HandEnd == HandEndMode.ToDeckShuffle && state.Deck.Count > 0
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
        var mode = EnemyTimeScope.VoidMode;
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
                VoidPollution.ApplyPenalty(creature, state, card, "抽到");
            }
        }

        return $"抽牌：{drawn} 张（虚无 {drawnVoids}，洗回 {reshuffles} 次，固定 {target} 张，口径 {mode}）";
    }

    /// <summary>把弃牌堆整体洗回抽牌堆（用怪物的 AI 随机流；拿不到 RNG 就保持原顺序）。</summary>
    private static void ReshuffleDiscardIntoDeck(Creature creature, EnemyIntentState state)
    {
        state.Deck.AddRange(state.Discard);
        state.Discard.Clear();
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

    public static Decision Decide(EnemyIntentState state)
    {
        // 规则层 v1.1 §2.3 第 3 条（**2026-10-14 用户澄清，取代旧的四档**）：
        //   ① 手牌里有「条件已满足」的条件意图 ⇒ **打它**（不论它在第几位）
        //      ⇒ 「条件」是**插队优先权**，不是"能不能打"的**限制**。
        //   ② 否则打**第一张意图**（**不再检查条件**：带条件 / 不带条件都照打）
        //   ③ 手牌里**没有任何意图**（空手 / 只剩虚无 / 全被吃掉）⇒ **眩晕**
        // 虚无（凋萎）不携带 MoveState ⇒ 永远不是候选，也不算"有意图可打"。
        var scan = new List<string>();

        // ① 第一个「条件已满足」的条件意图（**与位置无关**）
        for (var i = 0; i < state.Hand.Count; i++)
        {
            var card = state.Hand[i];
            if (card.IsVoid)
            {
                scan.Add($"{card.MoveId}:虚无→跳过");
                continue;
            }

            if (card.Eaten)
            {
                scan.Add($"{card.MoveId}:被虚无吃掉→跳过");
                continue;
            }

            if (card.Forced && card.ConditionMet)
            {
                return new Decision(
                    "条件插队",
                    card,
                    $"第 {i} 张条件意图 {card.MoveId} 条件已满足 ⇒ 插队直接打｜{string.Join(" / ", scan)}");
            }
        }

        // ② 第一张意图：条件**不再**是门槛（未满足 / 非条件意图都照打）
        for (var i = 0; i < state.Hand.Count; i++)
        {
            var card = state.Hand[i];
            if (card.IsVoid || card.Eaten)
            {
                continue;
            }

            return new Decision(
                "第一张意图",
                card,
                $"第 {i} 张 {card.Describe()} 是第一张意图 ⇒ 直接打（不看条件）｜{string.Join(" / ", scan)}");
        }

        // ③ 没有任何意图 ⇒ 眩晕
        return new Decision(
            "无意图→晕眩",
            null,
            state.Hand.Count == 0
                ? "手牌为空"
                : $"手牌里没有意图（{string.Join(" / ", scan)}）");
    }

    private static void Apply(Creature creature, EnemyIntentState state, Decision decision)
    {
        state.Decision = $"{decision.Kind}：{decision.Reason}";

        switch (decision.Kind)
        {
            case "无意图→晕眩":
                state.Pending = null;
                state.Decision += $"｜{IntentOps.StunForced(creature)}";
                return;
        }

        var card = decision.Card;
        if (card is null)
        {
            state.Pending = null;
            return;
        }

        var move = IntentDeck.Resolve(creature, card);
        if (move is null)
        {
            state.Pending = null;
            state.Decision += "｜解析不到 MoveState（跳过）";
            return;
        }

        state.Pending = card;
        state.Decision += $"｜{IntentOps.Rewrite(creature, move, force: true)}";
    }

    // ======================= 玩家动词（用户 2026-10-12 定稿：三个动作）=======================
    //
    // 「可以行动的」= 非虚无 + 没被虚无吃掉（「条件」自 2026-10-14 起**不再是门槛**，只是插队优先权）。
    // 三个动作都作用在**手牌**上，做完立刻 ReResolve（重算判定 + 重写 NextMove），
    // 所以卡带上的高亮/意图当场就会变。

    /// <summary>「推迟」：把**第一张"可以行动的"**意图移到手牌**最后**。</summary>
    public static string Postpone(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        var index = FirstActionableIndex(state);
        if (index < 0)
        {
            return NoActionable("推迟");
        }

        var card = state.Hand[index];
        state.Hand.RemoveAt(index);
        state.Hand.Add(card);

        return Report($"推迟 {card.MoveId}：手牌第 {index} 张 → 最后", creature);
    }

    /// <summary>「提前」：把**最后一张"可以行动的"**意图移到手牌**最前**。</summary>
    public static string Advance(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        var index = LastActionableIndex(state);
        if (index < 0)
        {
            return NoActionable("提前");
        }

        var card = state.Hand[index];
        state.Hand.RemoveAt(index);
        state.Hand.Insert(0, card);

        return Report($"提前 {card.MoveId}：手牌第 {index} 张 → 最前", creature);
    }

    /// <summary>「剥夺」：**丢弃第一张"可以行动的"**意图（进弃牌堆，本回合不再出现在手牌）。</summary>
    public static string Deprive(Creature creature)
    {
        var state = IntentDeck.Ensure(creature);
        var index = FirstActionableIndex(state);
        if (index < 0)
        {
            return NoActionable("剥夺");
        }

        var card = state.Hand[index];
        state.Hand.RemoveAt(index);
        state.Discard.Add(card);

        return Report($"剥夺 {card.MoveId}：手牌第 {index} 张 → 弃牌堆（弃牌堆 {state.Discard.Count} 张）", creature);
    }

    /// <summary>「可以行动的」判定（三个动词共用）。</summary>
    private static bool IsActionable(IntentCard card) => card.Playable;

    // ---------- 指向模式的"指定某一张"版本（用户 2026-10-12：动作卡打出后由玩家点某张意图）----------

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
        return Report($"推迟 {target.MoveId}：手牌第 {index} 张 → 最后", creature);
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
        return Report($"提前 {target.MoveId}：手牌第 {index} 张 → 最前", creature);
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
        return Report($"剥夺 {target.MoveId}：手牌第 {index} 张 → 弃牌堆（弃牌堆 {state.Discard.Count} 张）", creature);
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

        return IsActionable(target) || (allowWither && target.IsVoid);
    }

    private static string TargetRejected(string verb, IntentCard target)
    {
        var text = $"{verb}：目标 {target.MoveId} 不能选（不在手牌里 / 不可行动）";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }

    private static int FirstActionableIndex(EnemyIntentState state)
    {
        for (var i = 0; i < state.Hand.Count; i++)
        {
            if (IsActionable(state.Hand[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastActionableIndex(EnemyIntentState state)
    {
        for (var i = state.Hand.Count - 1; i >= 0; i--)
        {
            if (IsActionable(state.Hand[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string NoActionable(string verb)
    {
        var text = $"{verb}：手牌里没有「可以行动的」意图（非虚无 + 没被虚无吃掉）";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }

    private static string Report(string text, Creature creature)
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
        var decision = Decide(state);
        Apply(creature, state, decision);
        var text = $"重排 判定={decision.Kind}｜{IntentUiProbe.Describe(creature)}";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }
}
