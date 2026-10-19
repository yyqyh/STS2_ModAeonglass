using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>封笔</b>（NEW-23）· <b>1 费 · Uncommon · 技能 · 消耗</b> —— 给敌人当前手牌上[迟滞]，立刻洗牌并抽 5 张
/// （升级：抽 2 张）。该迟滞**只对这次洗牌生效**（2026-10-18 用户确认）。
/// </summary>
/// <remarks>
/// <para>
/// ① 敌人侧的「迟滞」是现成机制：<c>IntentCard.Stuck</c> ⇒ <c>IntentHandCycle.ReshuffleDiscardIntoDeck</c>
/// 会把带标记的意图<b>留在弃牌堆</b>（并有"全是迟滞就忽略一次"的无死锁兜底）。
/// </para>
/// <para>
/// ② "只对这次洗牌生效" = 洗牌后立刻把标记清掉，所以下一次洗牌它们照常参与。
/// </para>
/// <para>
/// ③ ⚠️ 手牌这里**固定进弃牌堆**（不跟全局的 <c>IntentRules.HandEnd</c> 口径）：封笔要的是"这次洗牌把它们留下"，
/// 若跟着口径进了抽牌堆，迟滞就没有意义了。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSealQuill() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Uncommon, TargetType.AnyEnemy)
        .Keyword(CardKeyword.Exhaust)
        .Var("Draw", DrawBase, upgradeDelta: -3m))
{
    /// <summary>抽几张（升级 5 → 2）。</summary>
    private const decimal DrawBase = 5m;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target)
        {
            return Task.CompletedTask;
        }

        var state = IntentDeck.Ensure(target);

        // ① 给"当前手牌"打上迟滞（虚无/已被吃掉的意图不算）。
        var marked = state.Hand.Where(card => !card.Stuck && card.Playable).ToList();
        foreach (var card in marked)
        {
            card.Stuck = true;
        }

        // ② 手牌 → 弃牌堆（固定这一档，理由见类注释 ③），立刻洗回一次（迟滞的留在弃牌堆）。
        state.Discard.AddRange(state.Hand);
        state.Hand.Clear();
        IntentHandCycle.ReshuffleDiscardIntoDeck(target, state);

        // ③ 再抽 N 张（抽空了会自动把弃牌堆洗回来接着抽）。
        var draw = (int)DynamicVars["Draw"].BaseValue;
        var drawn = IntentHandCycle.DrawHand(target, state, draw);

        // ④ 只对这次洗牌生效：清掉标记。
        foreach (var card in marked)
        {
            card.Stuck = false;
        }

        IntentOps.Touch(state);
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Intent} 封笔：{MonsterAccess.IdOf(target)} 手牌 {marked.Count} 张上迟滞"
            + $" → 立刻洗牌 → {drawn}");
        return Task.CompletedTask;
    }

}
