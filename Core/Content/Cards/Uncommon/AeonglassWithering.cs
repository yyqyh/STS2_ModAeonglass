using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

using STS2_Aeonglass.Core.Systems.Wither;
namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>枯萎</b>（CardNEW3，v2 二次更新）· <b>1 费 · Uncommon · 技能</b> —— 对一只敌人造成「它弃牌堆里[凋萎]数量 × 3」点伤害；升级把它的<b>抽牌堆</b>也算进去。
/// </summary>
/// <remarks>
/// ① 效果：数目标那一只敌人的凋萎数换伤害；升级把抽牌堆里的也算进来。
/// ② 差分：无差分。
/// ③ 依赖：凋萎计数（<c>WitherPool.WithersInDiscard</c> / <c>WithersInDeck</c>）+ 意图 敌人牌堆。
/// ④ ⚠️ 口径由卡池侧按"唯一读得通的解释"定为"只数目标那一个敌人"（不是全场）；凋萎现在会出现在抽/手/弃三处 ⇒ 升级档是实打实的加强。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassWithering() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Uncommon, TargetType.AnyEnemy)
        .Var("PerWither", DamagePerWither))
{
    /// <summary>每张凋萎换多少伤害（v2 定值 3）。</summary>
    private const decimal DamagePerWither = 3m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var withers = WitherPool.WithersInDiscard(cardPlay.Target);
        if (IsUpgraded)
        {
            withers += WitherPool.WithersInDeck(cardPlay.Target);
        }

        var damage = withers * DynamicVars["PerWither"].BaseValue;
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Wither} 枯萎：{MonsterAccess.IdOf(cardPlay.Target)} 凋萎 {withers} 张（升级={IsUpgraded}）"
            + $" ⇒ {damage} 伤");

        if (damage <= 0m)
        {
            return;
        }

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

}
