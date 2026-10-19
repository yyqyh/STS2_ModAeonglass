using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>拆招</b>（NEW-18）· <b>1 费 · Uncommon · 技能</b> —— 随机丢掉敌人的一张意图；丢的是攻击意图则抽 3 张。
/// </summary>
/// <remarks>
/// <para>
/// ① 指向：<c>TargetType.AnyEnemy</c> + <see cref="IIntentTargetCard" /> ⇒ 走既有"只能点意图卡"的指向链路
/// （点中的那张意图由 <c>IntentTargetRegistry</c> 侧表带回），执行口是 <see cref="IntentActionCard.ApplyPointed" />，
/// 与「提前 / 威吓 / 思维剥夺」完全同一套。
/// </para>
/// <para>
/// ② "随机丢一张"是新增的动词 <c>IntentMoves.DepriveRandom</c>（既有「剥夺」是"第一张可行动的"）。
/// </para>
/// <para>
/// ③ 攻击判定只认 <c>IntentType.Attack</c>（用户 2026-10-16 口径：处决不算）；抽牌数走 <c>DynamicVars["DrawIfAttack"]</c>，
/// 升级 +1。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCounterMove() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Uncommon, TargetType.AnyEnemy)
        .Var("DrawIfAttack", DrawIfAttackBase, upgradeDelta: 1m)),
    IIntentTargetCard
{
    /// <summary>丢到攻击意图时补抽几张（升级 +1）。</summary>
    private const decimal DrawIfAttackBase = 3m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var wasAttack = false;

        await IntentActionCard.ApplyPointed(
            Owner,
            cardPlay.Target,
            "拆招",
            (_, creature, card) =>
            {
                var text = IntentMoves.DepriveRandom(creature, out var attack);
                wasAttack = attack;
                return text;
            });

        if (!wasAttack)
        {
            return;
        }

        var draw = (int)DynamicVars["DrawIfAttack"].BaseValue;
        var drawn = (await CardPileCmd.Draw(choiceContext, draw, Owner)).Count();
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 拆招：丢掉的是攻击意图 ⇒ 抽 {drawn}/{draw} 张");
    }

}
