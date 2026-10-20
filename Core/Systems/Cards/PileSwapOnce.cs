using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>「倒仓」用：一次性交换抽牌堆与弃牌堆的**全部内容**。</summary>
/// <remarks>
/// ① 与乱流不同：乱流是"槽位对象长期对调"，这里只交换一次内容，随后恢复常规抽/弃。
/// ② 静默搬运（<c>RemoveInternal/AddInternal(..., silent: true)</c>）+ 两次 <c>InvokeContentsChanged</c>，
///    避免中间态触发其它牌堆监听器；顺序按两边原顺序保留。
/// ③ 两张堆可能为空 —— 空堆也照常完成交换（另一边的牌全部搬过去）。
/// </remarks>
internal static class PileSwapOnce
{
    public static bool Swap(Player player)
    {
        if (player.PlayerCombatState is not { } piles)
        {
            return false;
        }

        var draw = piles.DrawPile;
        var discard = piles.DiscardPile;
        if (ReferenceEquals(draw, discard))
        {
            return false;
        }

        var drawCards = draw.Cards.ToList();
        var discardCards = discard.Cards.ToList();

        foreach (var card in drawCards)
        {
            if (draw.Cards.Contains(card))
            {
                draw.RemoveInternal(card, true);
            }
        }

        foreach (var card in discardCards)
        {
            if (discard.Cards.Contains(card))
            {
                discard.RemoveInternal(card, true);
            }
        }

        foreach (var card in discardCards)
        {
            draw.AddInternal(card, -1, true);
        }

        foreach (var card in drawCards)
        {
            discard.AddInternal(card, -1, true);
        }

        draw.InvokeContentsChanged();
        discard.InvokeContentsChanged();

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 倒仓：抽牌堆 {drawCards.Count} ↔ 弃牌堆 {discardCards.Count} 已交换");
        return true;
    }
}
