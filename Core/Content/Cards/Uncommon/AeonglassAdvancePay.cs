using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>预支</b>（CardETR1，v2 + delta 20261015）· <b>0 费 · Uncommon · 技能</b> —— 抽 2 张牌；下回合少抽 <b>1</b> 张（升级：抽 3 张，下回合仍只少抽 1）。
/// </summary>
/// <remarks>
/// ① 效果：本回合抽 2（升级 3）；"下回合少抽 1"挂成账单（<c>AeonglassDrawDebtPower</c>，下一次抽牌时生效并自毁）。
/// ② 差分：无差分。
/// ③ 依赖：跨回合抽牌修正（<c>AeonglassDrawDebtPower</c>）。
/// ④ ⚠️ 2026-10-15 delta：挂账 2 → 1 ⇒ <b>净值 +1（升级 +2）</b>，「借 X 还 X」的口径已被用户推翻。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAdvancePay() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(0, CardRarity.Uncommon)
        .Var("Draw", DrawBase, upgradeDelta: 1m)
        .Var("Debt", DebtBase))
{
    private const decimal DrawBase = 2m;
    private const decimal DebtBase = 1m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var draw = DynamicVars["Draw"].BaseValue;
        var debt = DynamicVars["Debt"].BaseValue;

        await CardPileCmd.Draw(choiceContext, (int)draw, Owner);
        await PowerCmd.Apply<AeonglassDrawDebtPower>(choiceContext, Owner.Creature, debt, Owner.Creature, this);
    }

}
