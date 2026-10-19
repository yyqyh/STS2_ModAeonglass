using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>威吓</b>（CardINT2，v2 · 原「跳过」改名）· <b>1 费 · Uncommon · 技能</b> —— <b>指向一张意图</b>，把它移到敌人手牌<b>末尾</b>；升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// ① 效果：只重排顺序（把那意图往后压）；升级挂 [保留]。
/// ② 差分：无差分。
/// ③ 依赖：意图 敌人时间（<c>IntentMoves.Postpone</c>）+ 原版指向链路。
/// ④ ⚠️ v2 由「跳过」改名；目标类型必须用 <c>AnyEnemy</c>（同「提前」：只有生物目标类型才会走
/// 本体的 <c>SingleCreatureTargeting</c>，否则拖完立刻结算、拿不到指向）；候选只有"可行动的"意图。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassIntimidate() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Uncommon, TargetType.AnyEnemy)
        .UpgradeAddKeyword(CardKeyword.Retain)),
    IIntentTargetCard
{
    protected override Task OnPlay(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
        => IntentActionCard.ApplyPointed(
            Owner,
            cardPlay.Target,
            "威吓",
            (_, creature, card) => IntentMoves.Postpone(creature, card));

}
