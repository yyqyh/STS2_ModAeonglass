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
/// <b>透支</b>（CardETR2，v2 二次更新）：<b>0 费</b> · 本回合获得 2 点能量；下回合少 <b>1</b> 点能量
/// （升级：本回合 +3，下回合仍只少 1）。
/// </summary>
/// <remarks>用户 2026-10-14 二次改动：费用 1 → 0；挂账 2 → 1（升级档净值因此变成 +2）。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassOverdraft() : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal GainBase = 2m;
    private const decimal DebtBase = 1m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Gain", GainBase), new DynamicVar("Debt", DebtBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var gain = DynamicVars["Gain"].BaseValue;
        var debt = DynamicVars["Debt"].BaseValue;

        await PlayerCmd.GainEnergy(gain, Owner);
        await PowerCmd.Apply<AeonglassEnergyDebtPower>(choiceContext, Owner.Creature, debt, Owner.Creature, this);
    }

    /// <summary>只抬本回合能量（挂账固定 2）。</summary>
    protected override void OnUpgrade() => DynamicVars["Gain"].UpgradeValueBy(1m);
}
