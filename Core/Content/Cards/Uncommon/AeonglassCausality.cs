using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>因果</b>（CardRW2，v2）：1 费 · 9 伤（升级 12）；<b>本局游戏</b>你每回溯过一次 +3 伤（升级 +4）。
/// </summary>
/// <remarks>
/// <para>
/// <b>口径是"本局"而不是"本场"</b>（v2 改动）：读 <see cref="RunHistory.Rewinds" />，
/// 那个数存在起始遗物的 <c>[SavedProperty]</c> 上 ⇒ 跨战斗、跨读档累计，不会随战斗结束清零。
/// </para>
/// <para>加成的上限：v2 没写上限 ⇒ 不设上限（与年轮 / 刻痕 同口径）。</para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCausality() : ModCardTemplate(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const decimal DamageBase = 9m;
    private const decimal PerRewindBase = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(DamageBase, ValueProp.Move), new DynamicVar("PerRewind", PerRewindBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var rewinds = RunHistory.Rewinds(Owner);
        var bonus = rewinds * DynamicVars["PerRewind"].BaseValue;
        var damage = DynamicVars.Damage.BaseValue + bonus;

        Const.Logger.Info($"[{Const.ModId}] 因果：本局回溯 {rewinds} 次 ⇒ +{bonus}（合计 {damage}）");

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    /// <summary>升级：基础伤害 +3、每次回溯的加成 +1（9/3 → 12/4）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["PerRewind"].UpgradeValueBy(1m);
    }
}
