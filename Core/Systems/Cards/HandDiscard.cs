using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「从手牌选 N 张弃掉」的<b>共用口</b>。
/// </summary>
/// <remarks>
/// ① 职责：走 <c>CardSelectCmd</c> 选牌后逐张 <c>CardCmd.Discard</c>。
/// ② 调用时机：拨针等需要弃牌的卡。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class HandDiscard
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
