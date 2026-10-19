using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>因果</b>（CardRW2，v2）· <b>1 费 · Uncommon · 攻击</b> —— 造成 9 点伤害（升级 12）；<b>本局游戏</b>你每回溯过一次 +3 伤（升级 +4）。
/// </summary>
/// <remarks>
/// ① 效果：基础 9 伤 + 回溯次数 × 3；升级同时抬基础与每级加成。
/// ② 差分：无差分。
/// ③ 依赖：<c>RunHistory.Rewinds</c>（记在起始遗物的 <c>[SavedProperty]</c> 上 ⇒ 跨战斗、跨读档累计）。
/// ④ ⚠️ 口径是"本局"而不是"本场"（v2 改动）；<b>无上限</b>（与年轮 / 刻痕同口径）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCausality() : AeonglassCardTemplate(
    new AeonglassCardSpec(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
        .DamageVar(DamageBase, upgradeDelta: 3m)
        .Var("PerRewind", PerRewindBase, upgradeDelta: 1m))
{
    private const decimal DamageBase = 9m;
    private const decimal PerRewindBase = 3m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var rewinds = RunHistory.Rewinds(Owner);
        var bonus = rewinds * DynamicVars["PerRewind"].BaseValue;
        var damage = DynamicVars.Damage.BaseValue + bonus;

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 因果：本局回溯 {rewinds} 次 ⇒ +{bonus}（合计 {damage}）");

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

}
