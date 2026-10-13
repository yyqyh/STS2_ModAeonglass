using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>贯刺</b>（CardBA5）：1 费 · 10 伤 · 弃 1 张牌。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassImpale() : AeonglassAttackTemplate(1, CardRarity.Common, 10m, 3m)
{
    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await DiscardOneAsync(choiceContext, this);

    /// <summary>
    /// 从手牌选 1 张弃（照本体「飞匕」/「杂技」的写法：`CardSelectCmd.FromHandForDiscard` + `CardCmd.Discard`）。
    /// </summary>
    internal static async Task DiscardOneAsync(PlayerChoiceContext choiceContext, CardModel source)
    {
        var picked = (await CardSelectCmd.FromHandForDiscard(
                choiceContext,
                source.Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
                null,
                source))
            .FirstOrDefault();

        if (picked is not null)
        {
            await CardCmd.Discard(choiceContext, picked);
        }
    }
}
