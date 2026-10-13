using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>掩护</b>（CardBB3）：1 费 · 5 挡 · 抽 1 张。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCover() : AeonglassBlockTemplate(1, CardRarity.Common, 5m, 2m)
{
    protected override Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => CardPileCmd.Draw(choiceContext, 1, Owner);
}
