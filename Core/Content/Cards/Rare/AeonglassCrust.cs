using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>结壳</b>（CardSD7，能力）：1 费 · 每当你消耗至少 1 点沙砾：获得 2 点格挡（每回合 1 次；升级 +1）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCrust() : ModCardTemplate(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("SpendTriggerBlock", 2m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassCrustPower>(
            choiceContext, Owner.Creature, DynamicVars["SpendTriggerBlock"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["SpendTriggerBlock"].UpgradeValueBy(1m);
}
