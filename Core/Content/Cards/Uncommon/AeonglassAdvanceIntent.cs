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
/// ③ 依赖：意图 敌人时间（<c>IntentMoves.Advance</c>）+ 原版指向链路（<c>TargetType.TargetedNoCreature</c> / <c>IIntentTargetCard</c> / <c>IntentActionCard.ApplyPointed</c>）。
/// ④ ⚠️ 候选只有"可行动的"意图（与「威吓 / 思维剥夺」同口径）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAdvanceIntent() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.TargetedNoCreature),
    IIntentTargetCard
{
    protected override HashSet<CardTag> CanonicalTags => [];
    
    protected override Task OnPlay(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
        => IntentActionCard.ApplyPointed(Owner, "提前", (_, creature, card) => IntentMoves.Advance(creature, card));

    /// <summary>v2：升级改为获得[保留]（原版现成 API，见本体「恩膏」）。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
