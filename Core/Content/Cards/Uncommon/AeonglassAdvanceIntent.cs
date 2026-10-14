using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>提前</b>（CardINT1，v2）：1 费 · <b>指向一张意图</b>，把它移到敌人手牌最前；升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// 指向机器沿用 v1 就位的那套（<c>TargetedNoCreature</c> + <see cref="IIntentTargetCard" /> +
/// <c>IntentActionCard.ApplyPointed</c>）：打出后由本体的原版指向链路把"玩家点的那张意图"带回来。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAdvanceIntent() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.TargetedNoCreature),
    IIntentTargetCard
{
    protected override HashSet<CardTag> CanonicalTags => [];
    
    protected override Task OnPlay(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
        => IntentActionCard.ApplyPointed(Owner, "提前", (_, creature, card) => IntentResolver.Advance(creature, card));

    /// <summary>v2：升级改为获得[保留]（原版现成 API，见本体「恩膏」）。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
