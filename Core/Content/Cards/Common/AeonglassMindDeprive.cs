using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>思维剥夺</b>（CardINT3，v2 · 原「剥夺」改名）：1 费 · <b>指向一个敌人</b>，<b>随机</b>丢弃它一张意图；
/// 升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// 随机取"可行动的"意图再走定点接口 <c>IntentResolver.Deprive</c>；随机数用怪物自己的 AI 随机流
/// （跑局内可复现、回档也被快照覆盖）。被丢掉的那张**立刻进它的弃牌堆**（<c>Deprive</c> 的既有语义）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassMindDeprive() : ModCardTemplate(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 思维剥夺：没有目标怪");
            return Task.CompletedTask;
        }

        var state = IntentDeck.Ensure(target);
        var actionable = state.Hand.Where(card => card.Playable).ToList();
        if (actionable.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] 思维剥夺：{MonsterAccess.IdOf(target)} 手牌里没有可行动的意图");
            return Task.CompletedTask;
        }

        var index = target.Monster?.RunRng.MonsterAi is { } rng ? rng.NextInt(actionable.Count) : 0;
        Const.Logger.Info($"[{Const.ModId}] 思维剥夺（随机）：{IntentResolver.Deprive(target, actionable[index])}");
        return Task.CompletedTask;
    }

    /// <summary>v2：升级改为获得[保留]。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
