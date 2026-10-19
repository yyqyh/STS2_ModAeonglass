using MegaCrit.Sts2.Core.Entities.Cards;

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
public sealed class AeonglassFortify() : AeonglassCardTemplate(
    AeonglassCardSpec.Block(1, CardRarity.Uncommon, 6m, 2m)
        .Var("NextDefenseBonus", NextDefenseBonusBase, upgradeDelta: 1m)
        .Effect(CardEffects.ApplyPower<AeonglassNextDefensePower>("NextDefenseBonus")))
{
    private const decimal NextDefenseBonusBase = 3m;
}
