using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>岸击</b>（NEW-40，2026-10-20）· <b>2 费 · Rare · 攻击 · 搁浅 5</b> ——
/// 造成 1 点伤害，额外造成"所有卡组中搁浅牌数量之和 × 6"的伤害（升级 ×12）。
/// </summary>
/// <remarks>
/// ① 统计范围：生效副 + 非生效副 + 搁浅区（<c>StasisCount.CountAll</c>）。
/// ② 伤害 = 1 + 数量 × PerStrand；实时读，不缓存。
/// ③ 依赖：<see cref="AeonglassStasisCardTemplate" />（搁浅 5）+ <c>DamageThisPlay</c>。
/// ④ ⚠️ 交接单原文"6被伤害"按每张 ×6 记录。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassShoreStrike()
    : AeonglassStasisCardTemplate(
        AeonglassCardSpec.Attack(2, CardRarity.Rare, 1m)
            .Var("PerStrand", 6m, upgradeDelta: 6m),
        strandCombats: 5)
{
    protected override decimal DamageThisPlay =>
        DynamicVars["Damage"].BaseValue
        + StasisCount.CountAll(Owner) * DynamicVars["PerStrand"].BaseValue;
}
