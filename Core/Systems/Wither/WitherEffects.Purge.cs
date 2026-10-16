using MegaCrit.Sts2.Core.Entities.Creatures;

using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Systems.Wither;

/// <summary>
/// 凋萎的<b>清除</b>分册：三种清法（整只怪 / 手牌 / 弃牌堆）。
/// </summary>
/// <remarks>
/// ① 职责：<c>PurgeAll</c> / <c>PurgeFromHand</c> / <c>PurgeFromDiscard</c>。
/// ② 调用时机：「净化 / 时间主宰」等要清凋萎的卡。
/// ③ 依赖：<c>IntentDeck</c>（三堆）+ <c>WitherPool</c>（读数）。
/// ④ ⚠️ 三种清法都只记一笔 <c>VoidsSpent</c>（守恒式 deck+hand+discard+consumed+voidsSpent==total 的前提：别双计）。
/// </remarks>
internal static partial class WitherEffects
{
    /// <summary>
    /// 清掉这只怪**三个意图堆里的所有凋萎**（「净化」用），返回清掉几张。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PurgeFromDiscard" /> 的区别：那个只动弃牌堆（挑一张），这个把**整只怪身上的污染**一次清空。
    /// </remarks>
    public static int PurgeAll(Creature creature)
    {
        if (!IntentDeck.TryGet(creature, out var state))
        {
            return 0;
        }

        var purged = 0;
        foreach (var pile in new[] { state.Hand, state.Discard, state.Deck })
        {
            foreach (var card in pile.Where(c => c.IsVoid).ToList())
            {
                pile.Remove(card);
                purged++;
            }
        }

        state.VoidsSpent += purged;
        if (purged > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Wither} 净化：清空 {MonsterAccess.IdOf(creature)} 的全部凋萎 {purged} 张"
                + $"｜{state.Counts()}");
        }

        IntentOps.Touch(state);
        return purged;
    }

    /// <summary>
    /// 从该怪**手牌**里拿走最多 <paramref name="count" /> 张凋萎（进"消耗"，不再参与循环）。
    /// </summary>
    /// <remarks>这是"净化"要用的写入口；"剥夺"走的是另一条（进弃牌堆、会被洗回）—— 两者语义不同，都留着。</remarks>
    public static int PurgeFromHand(Creature creature, int count)
    {
        if (count <= 0 || !IntentDeck.TryGet(creature, out var state))
        {
            return 0;
        }

        var taken = 0;
        foreach (var card in state.Hand.Where(c => c.IsVoid).ToList())
        {
            if (taken >= count)
            {
                break;
            }

            state.Hand.Remove(card);
            // ⚠️ 只记一笔：`VoidsSpent` 的语义就是"结算/移除掉的凋萎"；
            // 再往 `Consumed` 记一次会让守恒式（deck+hand+discard+consumed+voidsSpent==total）**双计**。
            state.VoidsSpent++;
            taken++;
        }

        if (taken > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Wither} 净化：从手牌拿走 {taken} 张凋萎（剩 hand {WitherPool.WithersInHand(creature)}"
                + $" / discard {WitherPool.WithersInDiscard(creature)}）");
        }

        IntentOps.Touch(state);
        return taken;
    }

    /// <summary>
    /// 从<b>怪物弃牌堆</b>清掉最多 <paramref name="count" /> 张凋萎（WTH-3 净化用），返回实际清掉几张。
    /// </summary>
    /// <remarks>只动弃牌堆：手牌里的凋萎照旧要走"行动后结算"那条路（<see cref="PurgeFromHand" /> 是另一条）。</remarks>
    public static int PurgeFromDiscard(Creature creature, int count)
    {
        if (count <= 0 || !IntentDeck.TryGet(creature, out var state))
        {
            return 0;
        }

        var purged = 0;
        foreach (var card in state.Discard.Where(c => c.IsVoid).Take(count).ToList())
        {
            state.Discard.Remove(card);
            state.VoidsSpent++;
            purged++;
        }

        if (purged > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Wither} 净化：从弃牌堆清掉 {purged} 张凋萎｜{state.Counts()}");
        }

        IntentOps.Touch(state);
        return purged;
    }
}
