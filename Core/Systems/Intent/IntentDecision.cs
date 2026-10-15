using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Wither;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图的<b>判定</b>：当前这只怪该打出哪一张（含"直接打第一张"的兜底与首/末可行动定位）。
/// </summary>
/// <remarks>
/// ① 职责：<c>Decide</c> 出决策、<c>Apply</c> 落地；<c>Decision</c> 是决策载体。
/// ② 调用时机：回合流程（<c>IntentTurnFlow</c>）每回合算一次。
/// ③ 依赖：<c>IntentDeck</c>（三堆状态）+ <c>IntentRules</c>（三堆规则）。
/// ④ ⚠️ 判定一律不看 UI；"可行动"的定义只有 <c>IsActionable</c> 一处。
/// </remarks>
internal static class IntentDecision
{

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

    internal static void Apply(Creature creature, EnemyIntentState state, Decision decision)
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

        var move = IntentCycle.Resolve(creature, card);
        if (move is null)
        {
            state.Pending = null;
            state.Decision += "｜解析不到 MoveState（跳过）";
            return;
        }

        state.Pending = card;
        state.Decision += $"｜{IntentOps.Rewrite(creature, move, force: true)}";
    }

    internal static int FirstActionableIndex(EnemyIntentState state)
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

    internal static int LastActionableIndex(EnemyIntentState state)
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

    /// <summary>「可以行动的」判定（三个动词共用）。</summary>
    internal static bool IsActionable(IntentCard card) => card.Playable;
}
