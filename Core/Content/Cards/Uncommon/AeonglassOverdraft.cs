using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>透支</b>（CardETR2，v2 + delta 20261015）· <b>0 费 · Uncommon · 技能</b> —— 本回合获得 2 点能量；下回合少 <b>1</b> 点能量（升级：+3，下回合仍只少 1）。
/// </summary>
/// <remarks>
/// ① 效果：本回合 +2 能量（升级 +3）；"下回合少 1"挂成账单（<c>AeonglassEnergyDebtPower</c>）。
/// ② 差分：无差分。
/// ③ 依赖：跨回合能量修正（<c>AeonglassEnergyDebtPower</c>）。
/// ④ ⚠️ 2026-10-14 费用 1 → 0；2026-10-15 delta 挂账 2 → 1 ⇒ <b>净值 +1（升级 +2）</b>；0 费送 2 能量属"0 费大效果"，强度要盯。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassOverdraft() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(0, CardRarity.Uncommon)
        .Var("Gain", GainBase, upgradeDelta: 1m)
        .Var("Debt", DebtBase))
{
    private const decimal GainBase = 2m;
    private const decimal DebtBase = 1m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var gain = DynamicVars["Gain"].BaseValue;
        var debt = DynamicVars["Debt"].BaseValue;

        await PlayerCmd.GainEnergy(gain, Owner);
        await PowerCmd.Apply<AeonglassEnergyDebtPower>(choiceContext, Owner.Creature, debt, Owner.Creature, this);
    }

}
