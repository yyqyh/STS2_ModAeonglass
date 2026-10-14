using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>拨针</b>（CardUTL1）：0 费 · 抽 1 弃 1；<b>平衡档</b>改为抽 2 弃 1（升级不变，靠相位）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDialNudge() : ModCardTemplate(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var draw = SandPhases.IsBalanced(Owner) ? 2 : 1;
        await CardPileCmd.Draw(choiceContext, draw, Owner);
        await AeonglassImpale.DiscardOneAsync(choiceContext, this);
    }

    /// <summary>升级不变（卡表口径：靠相位而不是升级）。</summary>
    protected override void OnUpgrade()
    {
    }
}
