using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>透支</b>（CardETR2）：1 费 · 本回合获得 2 点能量；下回合少 2 点能量（升级 ⇒ 3/3）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassOverdraft() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal AmountBase = 2m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Borrow", AmountBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var borrow = DynamicVars["Borrow"].BaseValue;
        await PlayerCmd.GainEnergy(borrow, Owner);
        await PowerCmd.Apply<AeonglassEnergyDebtPower>(choiceContext, Owner.Creature, borrow, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["Borrow"].UpgradeValueBy(1m);
}
