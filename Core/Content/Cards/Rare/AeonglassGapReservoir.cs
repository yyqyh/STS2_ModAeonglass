using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>蓄隙</b>（CardGAP5，能力）：2 费 · 每回合开始时：若间隙里 ≥3 张牌，获得 3 点格挡（升级 +1）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassGapReservoir() : ModCardTemplate(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("GapBlock", 3m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassGapPulsePower>(
            choiceContext, Owner.Creature, DynamicVars["GapBlock"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["GapBlock"].UpgradeValueBy(1m);
}
