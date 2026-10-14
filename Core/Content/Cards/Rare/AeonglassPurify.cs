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
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>净化</b>（CardWTH3，v2 · <b>重写</b>）：1 费攻击牌 ·
/// 消耗<b>场上所有凋萎</b>，对<b>所有敌人</b>造成「凋萎数量 × 6」的伤害；
/// 升级：消耗范围扩到所有<b>状态牌</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>数量按"消耗前"结算</b>（用户 2026-10-14 定）：伤害用的是"数完之后的那个数"，
/// 升级档多清的那些状态牌不进这个数。
/// </para>
/// <para>
/// <b>"场上"包括玩家</b>：怪侧的凋萎在意图堆里（走 <c>VoidPollution</c>），
/// 玩家侧若也有（将来的机制塞进来的）走 <see cref="StatusSweeper" />；两边都算进数量、也都被清掉。
/// </para>
/// <para>凋萎现在是 <c>CardType.Status</c>（v2 设定）⇒ 升级档那句"所有状态牌"天然把它包括进去。</para>
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
        var monsterWithers = MonsterAccess.LivingEnemies.Sum(VoidPollution.WithersTotal);
        var playerWithers = StatusSweeper.Count(Owner, card => card is AeonglassWitherCard);
        var count = monsterWithers + playerWithers;

        // ② 消耗：怪侧全部凋萎 + 玩家侧全部凋萎（升级档再吃掉所有状态牌）。
        var purged = MonsterAccess.LivingEnemies.Sum(VoidPollution.PurgeAll);
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
