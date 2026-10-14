using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>省钟</b>（CardUTL2）：0 费 · 本回合下一张牌费用 −1（升级 ⇒ −2）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassFrugalChime() : ModCardTemplate(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("CostReduction", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassNextCardCostPower>(
            choiceContext, Owner.Creature, DynamicVars["CostReduction"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["CostReduction"].UpgradeValueBy(1m);
}
