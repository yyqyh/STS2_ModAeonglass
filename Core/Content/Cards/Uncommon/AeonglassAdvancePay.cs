using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>预支</b>（CardETR1，v2 + delta 20261015）：0 费 · 抽 2 张牌；下回合少抽 <b>1</b> 张
/// （升级：抽 3 张，<b>下回合仍只少抽 1</b>）。
/// </summary>
/// <remarks>
/// ⚠️ 2026-10-15 delta：挂账 2 → 1 ⇒ 升级档净值 <b>+2</b>（"借 X 还 X"的口径已被用户推翻）。
/// 挂账走 <see cref="AeonglassDrawDebtPower" />（下一次抽牌时生效并自毁）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAdvancePay() : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal DrawBase = 2m;
    private const decimal DebtBase = 1m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Draw", DrawBase), new DynamicVar("Debt", DebtBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var draw = DynamicVars["Draw"].BaseValue;
        var debt = DynamicVars["Debt"].BaseValue;

        await CardPileCmd.Draw(choiceContext, (int)draw, Owner);
        await PowerCmd.Apply<AeonglassDrawDebtPower>(choiceContext, Owner.Creature, debt, Owner.Creature, this);
    }

    /// <summary>只抬本回合抽数（挂账固定 1）。</summary>
    protected override void OnUpgrade() => DynamicVars["Draw"].UpgradeValueBy(1m);
}
