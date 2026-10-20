using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「从弃牌堆抽 N 张」的单一入口：按弃牌堆**堆顶顺序**取前 N 张进手牌。
/// </summary>
/// <remarks>
/// ① 本体没有"从弃牌堆抽"的命令 ⇒ 这里用 <c>CardPileCmd.Add(cards, Hand)</c> 搬运，
///    并逐张补发 <c>Hook.AfterCardDrawn</c> / <c>InvokeDrawn</c>，让"抽到牌"类能力照常响应。
/// ② 兜底：手牌上限、弃牌堆不足都按实际可搬张数截断（不洗牌、不从别处补）。
/// ③ 读的是 <c>PlayerCombatState.DrawPile/DiscardPile</c> **属性** ⇒ 乱流生效时跟着玩家看到的堆走。
/// </remarks>
internal static class DiscardDraw
{
    public static async Task<int> DrawAsync(
        PlayerChoiceContext context,
        Player player,
        int count)
    {
        if (count <= 0 || player.PlayerCombatState is not { } piles)
        {
            return 0;
        }

        var state = player.Creature.CombatState;
        if (state is null)
        {
            return 0;
        }

        var room = CardPile.MaxCardsInHand - piles.Hand.Cards.Count;
        var take = Math.Min(count, Math.Min(room, piles.DiscardPile.Cards.Count));
        if (take <= 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Host} 从弃牌堆抽：可抽 0 张"
                + $"（请求 {count}，手牌空位 {Math.Max(0, room)}，弃牌堆 {piles.DiscardPile.Cards.Count}）");
            return 0;
        }

        var cards = piles.DiscardPile.Cards.Take(take).ToList();
        await CardPileCmd.Add(cards, PileType.Hand);

        foreach (var card in cards)
        {
            try
            {
                await Hook.AfterCardDrawn(state, context, card, fromHandDraw: false);
                card.InvokeDrawn();
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Host} 弃牌堆抽牌后置钩子失败（忽略）："
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 从弃牌堆抽 {cards.Count} 张"
            + $"（请求 {count}，剩 {piles.DiscardPile.Cards.Count}）");
        return cards.Count;
    }
}
