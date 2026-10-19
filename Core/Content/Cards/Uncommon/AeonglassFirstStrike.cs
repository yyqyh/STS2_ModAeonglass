using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>先手</b>（CardBA9，v2）· <b>1 费 · Uncommon · 攻击</b> —— 造成 9 点伤害（升级 12）；若这是本回合你打出的第一张牌：改为 13 点（升级 16）。
/// </summary>
/// <remarks>
/// ① 效果：走攻击模板；"本回合第一张"命中时换用首张档伤害。
/// ② 差分：无差分。
/// ③ 依赖：<c>CombatHistory.CardsPlayedThisTurn</c>（本回合已出牌数 == 0）。
/// ④ ⚠️ 两档都要跟着升级涨（升级 +3 同时抬基础与首张档）；判据是"已打出张数"而不是"牌位"。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassFirstStrike() : AeonglassCardTemplate(
    AeonglassCardSpec.Attack(1, CardRarity.Uncommon, 9m, 3m)
        .Var("FirstDamage", FirstDamage, upgradeDelta: 3m))
{
    private const decimal FirstDamage = 13m;

    protected override decimal DamageThisPlay
        => CombatHistory.CardsPlayedThisTurn(Owner) == 0 ? FirstDamage + UpgradeDelta : base.DamageThisPlay;

    /// <summary>升级加的那 3 点（"第一张"那一档也跟着涨）。</summary>
    private decimal UpgradeDelta => CurrentUpgradeLevel * 3m;

}
