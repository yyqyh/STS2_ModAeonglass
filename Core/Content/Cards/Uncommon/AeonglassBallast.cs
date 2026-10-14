using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>压舱</b>（CardSTA2，能力）：2 费 · 每当你搁浅一张牌：获得 6 点格挡（每回合 1 次；升级 +1）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBallast() : ModCardTemplate(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("StrandBlock", 6m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassBallastPower>(
            choiceContext, Owner.Creature, DynamicVars["StrandBlock"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["StrandBlock"].UpgradeValueBy(1m);
}
