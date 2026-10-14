using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>剥夺</b>（CardINT3）：1 费 · 丢弃一张<b>随机</b>意图（进它的弃牌堆）。</summary>
/// <remarks>
/// 随机取"可行动的"意图，再走 M3 已有的定点接口 <c>IntentResolver.Deprive(creature, target)</c>；
/// 随机数用怪物自己的 AI 随机流（跑局内可复现、回档也被快照覆盖，与 M3 洗牌同一条流）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDepriveIntent() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (MonsterAccess.LivingEnemies.FirstOrDefault() is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 剥夺：场上没有活着的怪");
            return Task.CompletedTask;
        }

        var state = IntentDeck.Ensure(target);
        var actionable = state.Hand.Where(card => card.Playable).ToList();
        if (actionable.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] 剥夺：目标手牌里没有可行动的意图");
            return Task.CompletedTask;
        }

        var index = target.Monster?.RunRng.MonsterAi is { } rng
            ? rng.NextInt(actionable.Count)
            : 0;
        var picked = actionable[index];

        Const.Logger.Info($"[{Const.ModId}] 剥夺（随机）：{IntentResolver.Deprive(target, picked)}");
        return Task.CompletedTask;
    }
}
