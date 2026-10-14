using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>精神错乱</b>（NEW-2，v2）：1 费 · <b>指向一个敌人</b>，把它的整手意图**洗乱**；升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// 只洗顺序（不抽不弃、数量与内容都不变）⇒ 玩家仍然看得见那几张牌，只是猜不到它先打哪张。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassConfusion() : ModCardTemplate(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 精神错乱：没有目标怪");
            return Task.CompletedTask;
        }

        Const.Logger.Info($"[{Const.ModId}] {IntentResolver.Scramble(target)}");
        return Task.CompletedTask;
    }

    /// <summary>v2：升级改为获得[保留]。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
