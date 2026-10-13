using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>挡格</b>（CardBB5）：1 费 · 6 挡 · 弃 1 张牌。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassParry() : AeonglassBlockTemplate(1, CardRarity.Common, 6m, 3m)
{
    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await AeonglassImpale.DiscardOneAsync(choiceContext, this);
}
