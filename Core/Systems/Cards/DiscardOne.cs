using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「从手牌选 N 张弃掉」的共用口（v2 的拨针等牌用）。
/// </summary>
/// <remarks>
/// <para>
/// 原来这个方法是写在「贯刺」卡里的静态方法（<c>AeonglassImpale.DiscardOneAsync</c>）——
/// 那张卡在 v2 卡池里没有 ⇒ 抽成系统层工具，别再让"某张卡"承担公用逻辑。
/// </para>
/// <para>写法照本体「飞匕」/「杂技」：<c>CardSelectCmd.FromHandForDiscard</c> 选 → <c>CardCmd.Discard</c> 弃。</para>
/// </remarks>
internal static class DiscardOne
{
    /// <summary>从手牌选 <paramref name="count" /> 张弃掉（玩家取消 / 手牌不够就弃多少算多少）。</summary>
    public static async Task FromHandAsync(
        PlayerChoiceContext choiceContext,
        CardModel source,
        int count = 1)
    {
        if (count <= 0)
        {
            return;
        }

        var picked = await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            source.Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, count),
            null,
            source);

        foreach (var card in picked.ToList())
        {
            await CardCmd.Discard(choiceContext, card);
        }
    }
}
