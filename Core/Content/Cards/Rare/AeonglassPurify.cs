using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.Intent;

using STS2_Aeonglass.Core.Systems.Wither;
namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>净化</b>（CardWTH3，v2 · 重写）· <b>1 费 · Rare · 攻击（全体）</b> —— 消耗场上所有[凋萎]，对所有敌人造成「凋萎数量 × 6」的伤害；升级：消耗范围扩到所有<b>状态牌</b>。
/// </summary>
/// <remarks>
/// ① 效果：先数（消耗前）→ 再清（怪侧三个意图堆 + 玩家侧战斗牌堆）→ 再打全体伤害；升级档多清"所有状态牌"。
/// ② 差分：无差分。
/// ③ 依赖：凋萎（<c>WitherPool.WithersTotal</c>）+ 玩家侧清扫（<c>StatusSweeper</c>）。
/// ④ ⚠️ "数量按消耗前结算"（用户 2026-10-14 定）：升级档多清的那些状态牌<b>不进</b>这个数；"场上"<b>包含玩家侧</b>。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassPurify() : ModCardTemplate(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    /// <summary>每张凋萎换多少伤害（v2 定值 6）。</summary>
    private const decimal DamagePerWither = 6m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("PerWither", DamagePerWither)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 数"消耗前"的凋萎：怪侧（三个意图堆）+ 玩家侧（战斗牌堆）。
        var monsterWithers = MonsterAccess.LivingEnemies.Sum(WitherPool.WithersTotal);
        var playerWithers = StatusSweeper.Count(Owner, card => card is AeonglassWitherCard);
        var count = monsterWithers + playerWithers;

        // ② 消耗：怪侧全部凋萎 + 玩家侧全部凋萎（升级档再吃掉所有状态牌）。
        var purged = MonsterAccess.LivingEnemies.Sum(WitherPool.PurgeAll);
        purged += await StatusSweeper.ConsumeAsync(choiceContext, Owner, card => card is AeonglassWitherCard);
        if (IsUpgraded)
        {
            purged += await StatusSweeper.ConsumeAsync(choiceContext, Owner, card => card.Type == CardType.Status);
        }

        // ③ 对所有敌人造成 count × 6。
        var damage = count * DynamicVars["PerWither"].BaseValue;
        Const.Logger.Info(
            $"[{Const.ModId}] 净化：凋萎 {count} 张（怪 {monsterWithers} + 自己 {playerWithers}），清掉 {purged} 张"
            + $" ⇒ 对所有敌人 {damage} 伤");

        if (damage <= 0m || Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    /// <summary>升级只扩"消耗范围"（伤害公式不变）⇒ 卡面用升级追加句写清楚。</summary>
    protected override void OnUpgrade()
    {
    }

    protected override void AddExtraArgsToDescription(MegaCrit.Sts2.Core.Localization.LocString description)
    {
        base.AddExtraArgsToDescription(description);
        CardTextArgs.InjectUpgradeClause(description, this, IsUpgraded);
    }
}
