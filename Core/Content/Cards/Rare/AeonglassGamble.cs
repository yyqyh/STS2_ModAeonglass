using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>豪赌</b>（CardNEW1，v2）· <b>0 费 · Rare · 技能</b> · <b>迟滞</b> —— 眩晕一只敌人，并且<b>跳过你的下一个回合</b>；升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// ① 效果：眩晕目标（原生 <c>CreatureCmd.Stun</c>）+ 无条件跳过自己下回合（<c>AeonglassSkipNextTurnPower</c>）；升级挂 [保留]。
/// ② 差分：无差分。
/// ③ 依赖：本体眩晕 + 兜底 follow-up（<c>IntentOps.FallbackNextMoveId</c>）；迟滞关键词（不参与洗牌）。
/// ④ ⚠️ 眩晕<b>尊重本体免疫</b>：Boss 能晕、免疫眩晕的怪不生效（不学 <c>IntentOps.StunForced</c> 强行绕过，落没落看 <c>IsStunned</c>）；
///    代价与眩晕是<b>两笔</b>——无论眩晕有没有落地，你的下一个回合照样被跳过。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassGamble() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(0, CardRarity.Rare, TargetType.AnyEnemy)
        .Keyword(HysteresisKeyword.Value)
        .UpgradeAddKeyword(CardKeyword.Retain)),
    IHysteresisCard
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // ① 眩晕（原生接口 ⇒ VFX / STUNNED 图标 / follow-up 都保住，免疫由引擎判）。
        await CreatureCmd.Stun(cardPlay.Target, IntentOps.FallbackNextMoveId(cardPlay.Target));
        var stunned = cardPlay.Target.IsStunned;
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 豪赌：眩晕 {MonsterAccess.IdOf(cardPlay.Target)} = {stunned}"
            + (stunned ? string.Empty : "（被免疫/招牌招式挡住 ⇒ 这一半没生效）"));

        // ② 代价：跳过你的下一个回合（与眩晕是否落地无关）。
        await PowerCmd.Apply<AeonglassSkipNextTurnPower>(
            choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

}
