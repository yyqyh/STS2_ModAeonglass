using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Uncommon;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「倒置回手」：沙砾倒置成功后，把在抽牌堆 / 弃牌堆里的<b>倒影</b>拉回手牌。
/// </summary>
/// <remarks>
/// ① 职责：唯一入口 <c>PullAsync</c>，由 <c>SandInvertCmd</c> 在翻转通知之后调用。
/// ② 范围：只认 <see cref="AeonglassReflection" />；消耗堆不回手（消耗是永久移除），
///    打出中的 Play 堆也不碰（那张牌正在结算）。
/// ③ 口径：读的是 <c>PlayerCombatState</c> 的**属性**（乱流对调时玩家看到的堆就是这里取的堆）。
/// </remarks>
internal static class InvertRecall
{
    public static async Task PullAsync(Player player)
    {
        if (player.PlayerCombatState is not { } piles)
        {
            return;
        }

        var candidates = piles.DrawPile.Cards
            .Concat(piles.DiscardPile.Cards)
            .Where(card => card is AeonglassReflection)
            .ToList();

        foreach (var card in candidates)
        {
            try
            {
                await CardPileCmd.Add(card, PileType.Hand);
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Sand} 倒影：倒置回手（{card.Id.Entry}）");
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Sand} 倒影回手失败（忽略）：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
