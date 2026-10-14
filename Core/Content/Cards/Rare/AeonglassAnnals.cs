using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>编年</b>（CardANN4，能力）：2 费 · 每回合开始时：若你上回合打出的牌 ≤1 张，获得 4 点格挡（升级 +1）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAnnals() : ModCardTemplate(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("QuietBlock", 4m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassAnnalsPower>(
            choiceContext, Owner.Creature, DynamicVars["QuietBlock"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["QuietBlock"].UpgradeValueBy(1m);
}
