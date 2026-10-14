using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>垒壁</b>（CardBB4，v2）：1 费 · 6 挡（升级 8）· 你的下一张防御牌 +3 点格挡（升级 +4）。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassRampart() : AeonglassBlockTemplate(1, CardRarity.Common, 6m, 2m)
{
    private const decimal NextDefenseBonusBase = 3m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(Block, ValueProp.Move), new DynamicVar("NextDefenseBonus", NextDefenseBonusBase)];

    /// <summary>v2：升级只抬"下一张防御牌"的增量（+3 → +4），格挡照模板走。</summary>
    protected override void OnUpgrade()
    {
        base.OnUpgrade();
        DynamicVars["NextDefenseBonus"].UpgradeValueBy(1m);
    }

    /// <summary>加成做成**可见的 buff**（<see cref="AeonglassNextDefensePower" />），不再用隐藏标记。</summary>
    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassNextDefensePower>(
            choiceContext, Owner.Creature, DynamicVars["NextDefenseBonus"].BaseValue, Owner.Creature, this);
}
