using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>精神错乱</b>（CardNEW2，v2）· <b>1 费 · Common · 技能</b> —— <b>指向一个敌人</b>，把它的整手意图洗乱；升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// ① 效果：只洗顺序（不抽不弃、数量与内容都不变）；升级给这张牌自己挂 [保留]。
/// ② 差分：无差分。
/// ③ 依赖：意图 敌人时间（<c>IntentMoves.Scramble</c>）。
/// ④ ⚠️ v2 新增卡；"洗乱"是干扰型效果，强度取决于玩家对意图的依赖度（本卡池的意图线依赖很高）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassConfusion() : ModCardTemplate(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 精神错乱：没有目标怪");
            return Task.CompletedTask;
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {IntentMoves.Scramble(target)}");
        return Task.CompletedTask;
    }

    /// <summary>v2：升级改为获得[保留]。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
