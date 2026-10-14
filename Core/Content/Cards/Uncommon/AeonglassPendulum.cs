using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>摆钟</b>（CardUTL4，能力）：1 费 · 每回合开始时抽 1 弃 1（升级 ⇒ 2/2）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassPendulum() : ModCardTemplate(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("TurnDraw", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassPendulumPower>(
            choiceContext, Owner.Creature, DynamicVars["TurnDraw"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["TurnDraw"].UpgradeValueBy(1m);
}
