using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>威吓</b>（CardINT2，v2 · 原「跳过」改名）：1 费 · <b>指向一张意图</b>，把它移到敌人手牌末尾；
/// 升级 → 获得[保留]。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassIntimidate() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.TargetedNoCreature),
    IIntentTargetCard
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(
        MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
        => IntentActionCard.ApplyPointed(Owner, "威吓", (_, creature, card) => IntentResolver.Postpone(creature, card));

    /// <summary>v2：升级改为获得[保留]。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
