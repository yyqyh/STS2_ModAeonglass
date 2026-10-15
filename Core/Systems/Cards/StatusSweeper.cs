using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「把玩家战斗牌堆里符合条件的牌**全部消耗掉**」的共用口（v2「净化」用）。
/// </summary>
/// <remarks>
/// <para>
/// 为什么需要它：净化要清的是"场上所有人"的凋萎 / 状态牌 —— 怪侧走
/// <c>WitherPool</c>（意图堆不是 <c>CardModel</c>），<b>玩家侧</b>才是真正的卡模型堆（手牌 / 抽 / 弃 / 消耗），
/// 这一份负责后者。
/// </para>
/// <para>消耗走本体的 <c>CardCmd.Exhaust</c>（进消耗堆、不再参与循环），表现与本体一致。</para>
/// </remarks>
internal static class StatusSweeper
{
    /// <summary>这张玩家现在有几张符合条件的牌（只看战斗牌堆：手牌 / 抽牌堆 / 弃牌堆 / 消耗堆）。</summary>
    public static int Count(Player player, Func<CardModel, bool> match) => Collect(player, match).Count;

    /// <summary>把这些牌全部消耗掉，返回实际消耗了几张。</summary>
    public static async Task<int> ConsumeAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        Func<CardModel, bool> match)
    {
        var targets = Collect(player, match);
        foreach (var card in targets)
        {
            await CardCmd.Exhaust(choiceContext, card);
        }

        return targets.Count;
    }

    private static List<CardModel> Collect(Player player, Func<CardModel, bool> match)
    {
        var result = new List<CardModel>();
        foreach (var type in new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust })
        {
            if (type.GetPile(player) is { } pile)
            {
                result.AddRange(pile.Cards.Where(match));
            }
        }

        return result;
    }
}
