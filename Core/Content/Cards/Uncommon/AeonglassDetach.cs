using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;
using STS2_Aeonglass.Core.Systems.Wither;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>抽离</b>（NEW-19）· <b>1 费 · Uncommon · 技能</b> —— 把敌人抽牌堆的牌全放进它的弃牌堆，再塞 3 张[凋萎]（升级 4 张）。
/// </summary>
/// <remarks>
/// ① 口径（2026-10-18 台账）：原文没写"放进弃牌堆的张数" ⇒ 设计侧按<b>全部</b>记录
/// （<c>vars: discard_enemy_draw=all</c>）。
/// ② 两张操作都只动<b>敌人的意图三堆</b>（<c>IntentDeck</c>），不碰玩家的牌；每次都 <c>IntentOps.Touch</c>
/// 让卡带刷新。
/// ③ 依赖：<c>IntentDeck.Ensure</c>（三堆）+ <c>WitherEffects.Inject</c>（塞凋萎的单一收口点）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDetach() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Uncommon, TargetType.AnyEnemy)
        .Var("Wither", WitherBase, upgradeDelta: 1m))
{
    /// <summary>塞几张凋萎（升级 +1）。</summary>
    private const decimal WitherBase = 3m;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target)
        {
            return Task.CompletedTask;
        }

        var state = IntentDeck.Ensure(target);

        // ① 抽牌堆 → 弃牌堆（全部）。
        var moved = state.Deck.ToList();
        state.Deck.Clear();
        state.Discard.AddRange(moved);

        // ② 再塞凋萎（走唯一收口点，会带上"永时副 +1"之类被动）。
        var wither = (int)DynamicVars["Wither"].BaseValue;
        var injected = WitherEffects.Inject(target, wither);
        IntentOps.Touch(state);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Intent} 抽离：{MonsterAccess.IdOf(target)} 抽牌堆 {moved.Count} 张 → 弃牌堆"
            + $"｜再塞凋萎 ×{wither}：{injected}");
        return Task.CompletedTask;
    }

}
