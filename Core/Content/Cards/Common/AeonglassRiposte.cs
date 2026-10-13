using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>回身击</b>（CardBA3）：1 费 · 6 伤 · 抽 1 张。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassRiposte() : AeonglassAttackTemplate(1, CardRarity.Common, 6m, 2m)
{
    protected override Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => CardPileCmd.Draw(choiceContext, 1, Owner);
}
