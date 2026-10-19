using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Content.Cards.Uncommon;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「断刻」的回合结束打标：玩家回合结束时给所有<b>断刻</b>副本打上迟滞。
/// </summary>
/// <remarks>
/// ① 职责：唯一入口 <c>MarkAll</c>，由 <c>AeonglassCore.AfterSideTurnEnd</c> 调用。
/// ② 范围：只认 <see cref="AeonglassCutTick" />；打出时那张自己会 <c>Unmark</c>（"打出后失去迟滞"）。
/// ③ 口径：运行时标记只影响洗牌判定，不改卡面关键词（已知限制，同揭幕）。
/// </remarks>
internal static class CutTickMarking
{
    public static void MarkAll(Player player)
    {
        if (player.PlayerCombatState is not { } piles)
        {
            return;
        }

        var marked = 0;
        foreach (var card in piles.DrawPile.Cards
                     .Concat(piles.Hand.Cards)
                     .Concat(piles.DiscardPile.Cards)
                     .Concat(piles.ExhaustPile.Cards))
        {
            if (card is AeonglassCutTick)
            {
                Hysteresis.Mark(card);
                marked++;
            }
        }

        if (marked > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Host} 断刻：回合结束给 {marked} 张打上迟滞");
        }
    }
}
