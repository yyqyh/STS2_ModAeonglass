using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;



namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 双卡组的<b>牌堆重建</b>：切副后交换两副母本、把战斗牌堆按新副重建。
/// </summary>
/// <remarks>
/// ① 职责：<c>SwapMasters</c> + <c>RebuildCombatPilesAsync</c>（抽/弃/手牌全部对齐新副）。
/// ② 调用时机：<c>DeckSwitchService</c> 的切换流程中（切完必须重建）。
/// ③ 依赖：<c>DeckPiles</c>（堆访问）+ <c>DeckState</c>（存档状态）。
/// ④ ⚠️ 重建期间会静默搬牌，别在中间再触发一次切换。
/// </remarks>
internal static class DeckPileRebuild
{

    /// <summary>
    /// 静默搬运母本：把 <c>player.Deck</c> 与"非生效牌堆"的内容**互换**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 全走 <c>CardPile.RemoveInternal</c>/<c>AddInternal</c>（同步、静默）——
    /// 不用 <c>CardPileCmd.RemoveFromDeck</c>：那会为每张牌记地图历史、发移除钩子、播删除动画，
    /// 而"换一副牌"不是"删一堆牌"。也正因为没走那条路，母本的 <c>HasBeenRemovedFromState</c>
    /// 始终是 false，放回时不需要清标志（搁浅 那套标志处理这里用不上）。
    /// </para>
    /// <para>
    /// 两张母本全程都没离开 <c>RunState</c>（只是换了个牌堆），所以**不需要** <c>AddCard/RemoveCard</c>。
    /// </para>
    /// </remarks>
    internal static bool SwapMasters(Player player)
    {
        var pile = DeckPiles.InactivePileOf(player);
        if (pile is null)
        {
            return false;
        }

        var deck = player.Deck;
        var deckCards = deck.Cards.ToList();
        var pileCards = pile.Cards.ToList();

        // 先把两边都摘空，再互填 —— 避免"同一步里既读又写"造成张数错位。
        foreach (var card in deckCards)
        {
            if (deck.Cards.Contains(card))
            {
                deck.RemoveInternal(card, true);
            }
        }

        foreach (var card in pileCards)
        {
            if (pile.Cards.Contains(card))
            {
                pile.RemoveInternal(card, true);
            }
        }

        foreach (var card in pileCards)
        {
            if (!deck.Cards.Contains(card))
            {
                deck.AddInternal(card, -1, true);
            }
        }

        foreach (var card in deckCards)
        {
            if (!pile.Cards.Contains(card))
            {
                pile.AddInternal(card, -1, true);
            }
        }

        // 两边的 UI（顶栏牌组按钮、第二副牌堆按键）都要知道内容变了。
        deck.InvokeContentsChanged();
        pile.InvokeContentsChanged();
        return true;
    }

    /// <summary>
    /// 战斗内热切换：把本场战斗的牌堆按新卡组重铺一遍。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 照堕化拉撒路已实机验证过的路径：**只清 <c>PlayerCombatState.AllPiles</c>**
    /// （手牌 / 抽牌 / 弃牌 / 消耗 / 出牌区），<b>不动主卡组</b>；旧手牌是"**移出战斗**"
    /// （<c>CardPileCmd.RemoveFromCombat</c>），<b>不是弃牌、也不是消耗</b> —— 不触发任何弃牌 / 消耗类效果。
    /// </para>
    /// <para>
    /// 重建走本体的 <c>player.PopulateCombatState(rng, combatState)</c>（战斗开始时的同一条流程：
    /// 克隆卡组 → 进抽牌堆 → 洗牌），随后补抽一手（张数 = 换之前的手牌数，没有就按本体的 5 张）。
    /// </para>
    /// <para>
    /// ⚠️ 间隙的间隙牌堆与 双卡组的第二副牌组都是 <c>RunPersistent</c>，**不在** <c>AllPiles</c> 里
    /// ⇒ 热切换不会动它们（"A 组的牌可以躺在 B 的手牌里"这条口径因此天然成立）。
    /// </para>
    /// </remarks>
    internal static async Task RebuildCombatPilesAsync(Player player, PlayerChoiceContext? choiceContext)
    {
        if (!CombatManager.Instance.IsInProgress
            || player.PlayerCombatState is not { } combatPiles
            || player.Creature.CombatState is not CombatState combatState)
        {
            return;
        }

        var handBefore = combatPiles.Hand.Cards.Count;

        // ① 本场战斗里现有的牌**全部移出战斗**（不是弃牌 / 消耗）。
        var combatCards = combatPiles.AllPiles.SelectMany(pile => pile.Cards).ToList();
        if (combatCards.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(combatCards, skipVisuals: false);
        }

        // ② 用新卡组重铺（克隆 → 进抽牌堆 → 洗牌）。
        player.PopulateCombatState(player.RunState.Rng.Shuffle, combatState);

        foreach (var pile in combatPiles.AllPiles)
        {
            pile.InvokeContentsChanged();
        }

        // ③ 补抽一手；没有出牌上下文就不抽，等下一次自然抽牌。
        if (choiceContext is null)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 热切换完成（没有出牌上下文，未补抽手牌）");
            return;
        }

        var drawCount = handBefore > 0 ? handBefore : CombatManager.baseHandDrawCount;
        var drawn = (await CardPileCmd.Draw(choiceContext, drawCount, player)).Count();
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 热切换完成，补抽 {drawn}/{drawCount} 张");
    }
}
