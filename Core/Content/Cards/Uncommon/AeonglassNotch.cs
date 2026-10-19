using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>刻痕</b>（CardANN2，v2）· <b>1 费 · Uncommon · 攻击</b> —— 造成 8 点伤害（升级 11）；上回合你每打出 1 张牌：+3 点伤害（升级每张 +4）。
/// </summary>
/// <remarks>
/// ① 效果：基础 8 伤 + 上回合出牌数 × 3；升级同时抬基础与每张加成。
/// ② 差分：无差分。
/// ③ 依赖：<c>CombatHistory.CardsPlayedLastTurn</c>。
/// ④ ⚠️ v2 去掉"只算攻击牌"的限定与硬上限 ⇒ <b>无上限、不限卡型</b>（与年轮同口径，越拖越强）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassNotch() : AeonglassCardTemplate(
    new AeonglassCardSpec(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        .DamageVar(BaseDamage, upgradeDelta: 3m)
        .Var("PerAttack", PerAttack, upgradeDelta: 1m))
{
    private const decimal BaseDamage = 8m;
    private const decimal PerAttack = 3m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // v2：口径从"上回合的攻击牌数 + 硬上限"改成"上回合打出的**任意**牌数、无上限"。
        var played = CombatHistory.CardsPlayedLastTurn(Owner);
        var bonus = played * DynamicVars["PerAttack"].BaseValue;
        var damage = DynamicVars.Damage.BaseValue + bonus;

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 刻痕：上回合打出 {played} 张 ⇒ +{bonus}（合计 {damage}，v2：无上限）");
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

}
