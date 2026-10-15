using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>思维剥夺</b>（CardINT3，v2 · 原「剥夺」改名）· <b>1 费 · Common · 技能</b> —— <b>指向一个敌人</b>，<b>随机</b>丢弃它一张意图；升级 → 获得[保留]。
/// </summary>
/// <remarks>
/// ① 效果：随机挑一张"可行动的"意图，走定点接口丢弃（被丢弃的那张<b>立刻进它的弃牌堆</b>）；升级给这张牌自己挂 [保留]。
/// ② 差分：无差分。
/// ③ 依赖：意图 敌人时间（<c>IntentDeck</c> / <c>IntentMoves.Deprive</c>）；随机数用怪物自己的 AI 随机流（跑局内可复现、回档被快照覆盖）。
/// ④ ⚠️ v2 由「剥夺」改名；不可行动的意图不在候选里（与「提前 / 威吓」的候选口径一致）。
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
        Const.Logger.Info($"[{Const.ModId}] 思维剥夺（随机）：{IntentMoves.Deprive(target, actionable[index])}");
        return Task.CompletedTask;
    }

    /// <summary>v2：升级改为获得[保留]。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
