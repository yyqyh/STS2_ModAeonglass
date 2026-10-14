using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>豪赌</b>（NEW-1，v2）：0 费 · <b>迟滞</b> · 眩晕一只敌人，<b>跳过你的下一个回合</b>；升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// <para>
/// <b>眩晕尊重本体的免疫规则</b>：走原生 <c>CreatureCmd.Stun</c>（带兜底 follow-up 状态），
/// 那套"招牌招式不能被晕"的判定由引擎自己挡（不学 <c>IntentOps.StunForced</c> 去强行绕过）——
/// 所以对 Boss 能生效、对免疫眩晕的怪**不生效**。落没落看事后的 <c>Creature.IsStunned</c>，日志里写清楚。
/// </para>
/// <para>代价与眩晕是两笔：**无论眩晕有没有落地，你的下一个回合照样被跳过**（卡面写的就是这个交换）。</para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassGamble() : ModCardTemplate(0, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy),
    IHysteresisCard
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // ① 眩晕（原生接口 ⇒ VFX / STUNNED 图标 / follow-up 都保住，免疫由引擎判）。
        await CreatureCmd.Stun(cardPlay.Target, IntentOps.FallbackNextMoveId(cardPlay.Target));
        var stunned = cardPlay.Target.IsStunned;
        Const.Logger.Info(
            $"[{Const.ModId}] 豪赌：眩晕 {MonsterAccess.IdOf(cardPlay.Target)} = {stunned}"
            + (stunned ? string.Empty : "（被免疫/招牌招式挡住 ⇒ 这一半没生效）"));

        // ② 代价：跳过你的下一个回合（与眩晕是否落地无关）。
        await PowerCmd.Apply<AeonglassSkipNextTurnPower>(
            choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    /// <summary>v2：升级改为获得[保留]。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
