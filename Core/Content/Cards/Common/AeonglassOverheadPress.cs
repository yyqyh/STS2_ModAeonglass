using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>压顶</b>（CardBA6）：2 费 · 17 伤 · 弃 1 张牌。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassOverheadPress() : AeonglassAttackTemplate(2, CardRarity.Common, 17m, 4m)
{
    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await AeonglassImpale.DiscardOneAsync(choiceContext, this);
}
