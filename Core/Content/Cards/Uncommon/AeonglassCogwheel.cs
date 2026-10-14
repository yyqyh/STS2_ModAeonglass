using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>齿车</b>（CardUTL5，能力）：1 费 · 每当你打出 0 费牌：获得 1 点能量（每回合 1 次；升级 ⇒ 2 点）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCogwheel() : ModCardTemplate(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("ZeroCostEnergy", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassCogwheelPower>(
            choiceContext, Owner.Creature, DynamicVars["ZeroCostEnergy"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["ZeroCostEnergy"].UpgradeValueBy(1m);
}
