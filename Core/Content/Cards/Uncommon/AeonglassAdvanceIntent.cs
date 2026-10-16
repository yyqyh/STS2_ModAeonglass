using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>提前</b>（CardINT1，v2）· <b>1 费 · Uncommon · 技能</b> —— <b>指向一张意图</b>，把它移到敌人手牌<b>最前</b>；升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// ① 效果：只重排敌人意图手牌的顺序（不抽不弃）；升级给这张牌自己挂 [保留]。
/// ② 差分：无差分。
/// ③ 依赖：意图 敌人时间（<c>IntentMoves.Advance</c>）+ 原版指向链路（<c>TargetType.AnyEnemy</c> / <c>IIntentTargetCard</c> / <c>IntentActionCard.ApplyPointed</c>）。
/// ④ ⚠️ <b>必须是 <c>AnyEnemy</c> 这类"生物目标"类型</b>：本体 <c>NMouseCardPlay.TargetSelection</c> 只对
/// <c>AnyEnemy</c> / <c>AnyAlly</c> 调 <c>SingleCreatureTargeting</c>（= 正常选目标那套）；用
/// <c>TargetedNoCreature</c> 会掉进 <c>MultiCreatureTargeting</c>（不等待点目标、拖完立刻结算），
/// 于是永远拿不到指向结果（实测：log 里指向链路 0 次、"没有指向到任何意图" 6 次）。
/// 「只能选中意图卡」由 <c>AimingOurCard</c>（<c>IIntentTargetCard</c> 标记）+ <c>AllowedToTargetNode</c> 补丁挡住敌人节点实现。
/// 候选只有"可行动的"意图（与「威吓 / 思维剥夺」同口径）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAdvanceIntent() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy),
    IIntentTargetCard
{
    protected override HashSet<CardTag> CanonicalTags => [];
    
    protected override Task OnPlay(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
        => IntentActionCard.ApplyPointed(
            Owner,
            cardPlay.Target,
            "提前",
            (_, creature, card) => IntentMoves.Advance(creature, card));

    /// <summary>v2：升级改为获得[保留]（原版现成 API，见本体「恩膏」）。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
