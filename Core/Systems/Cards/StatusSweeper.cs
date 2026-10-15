using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「把玩家战斗牌堆里符合条件的牌<b>全部消耗</b>」的共用口。
/// </summary>
/// <remarks>
/// ① 职责：计数与清扫（净化等卡用）。
/// ② 调用时机：卡牌效果。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
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
