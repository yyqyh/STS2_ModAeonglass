using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>防护</b>（CardBB4，v2 + delta 20261015 · 原「垒壁」改名）· <b>1 费 · Uncommon · 技能</b> —— 获得 6 点格挡（升级 8）；你的下一张防御牌 +3 点格挡（升级 +4）。
/// </summary>
/// <remarks>
/// ① 效果：格挡走模板；额外挂一个"下一张防御牌 +N"的<b>可见 buff</b>。
/// ② 差分：无差分。
/// ③ 依赖：<c>AeonglassNextDefensePower</c>（认本体的格挡钩子）+ <c>CardTag.Defend</c>（模板自带）。
/// ④ ⚠️ 2026-10-15 delta 只改稀有度：Common → <b>Uncommon</b>；升级只抬"下一张防御牌"那一档。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassFortify() : AeonglassBlockTemplate(1, CardRarity.Uncommon, 6m, 2m)
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
