using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>预支</b>（CardETR1）：0 费 · 抽 2 张牌；下回合少抽 2 张（升级 ⇒ 3/3）。</summary>
/// <remarks>净值 0：借 X 还 X —— 挂账走 <see cref="AeonglassDrawDebtPower" />（下一次抽牌时生效并自毁）。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAdvancePay() : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal AmountBase = 2m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Borrow", AmountBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var borrow = DynamicVars["Borrow"].BaseValue;
        await CardPileCmd.Draw(choiceContext, (int)borrow, Owner);
        await PowerCmd.Apply<AeonglassDrawDebtPower>(choiceContext, Owner.Creature, borrow, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["Borrow"].UpgradeValueBy(1m);
}
