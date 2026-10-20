using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>弃葬</b>（NEW-38，2026-10-20）· <b>3 费 · Rare · 攻击</b> · <b>保留</b> ——
/// 造成 1 点伤害，弃牌堆每有 1 张牌额外造成 4 点伤害（升级 6）。
/// </summary>
/// <remarks>
/// ① 伤害 = 1 + 弃牌堆张数 × PerCard；实时读 <c>DiscardPile.Cards.Count</c>。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：统一模板的 <c>DamageThisPlay</c> 逃生口（数值卡面仍显示公式，不写死总伤）。
/// ④ ⚠️ 乱流生效时读的是玩家看到的弃牌堆（属性语义）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBurial()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Attack(3, CardRarity.Rare, 1m)
            .Keyword(CardKeyword.Retain)
            .Var("PerCard", 4m, upgradeDelta: 2m))
{
    protected override decimal DamageThisPlay =>
        DynamicVars["Damage"].BaseValue
        + (Owner.PlayerCombatState?.DiscardPile.Cards.Count ?? 0) * DynamicVars["PerCard"].BaseValue;
}
