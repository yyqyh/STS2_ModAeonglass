using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>相位律</b>（CardPH7，差分 · 能力）：1 费 · 每回合开始时按相位给收益（详见能力牌文案）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassPhaseLaw() : ModCardTemplate(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("GravelBlock", 3m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassPhaseLawPower>(
            choiceContext, Owner.Creature, DynamicVars["GravelBlock"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["GravelBlock"].UpgradeValueBy(1m);
}
