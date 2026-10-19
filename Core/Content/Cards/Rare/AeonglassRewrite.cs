using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>复写</b>（NEW-22）· <b>1 费 · Rare · 技能 · 消耗 · 迟滞</b> —— 给予一张手牌「重放 1」与「保留」。
/// </summary>
/// <remarks>
/// <para>
/// ① 「重放」是<b>官方关键词</b>（2026-10-18 用户确认，不需要我们再定义）⇒ 直接写
/// <c>CardModel.BaseReplayCount = 1</c>（setter 会发 <c>ReplayCountChanged</c>，卡面与伤害预览自动跟上）。
/// </para>
/// <para>
/// ② 「迟滞」= 卡定义实现 <see cref="IHysteresisCard" />（洗牌跳过、留弃牌堆），与乱流/豪赌同一套。
/// </para>
/// <para>
/// ③ 选择界面：借本体 <c>CardSelectCmd.FromHand</c>；提示文案用我们自己的键
/// <c>STS2_AEONGLASS_PROMPT_REWRITE</c>（在 <c>static_hover_tips</c> 表里，与既有 PROMPT_* 同命名），不用本体的"升级/附魔"那几句。
/// ⚠️ headless 测试没有应答选择界面的能力 ⇒ 这张卡只能实机验（与拨针/沙漏倾倒同一类空白）。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassRewrite() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Rare)
        .Keyword(CardKeyword.Exhaust)
        .Keyword(HysteresisKeyword.Value)
        .UpgradeRemoveKeyword(CardKeyword.Exhaust)),
    IHysteresisCard
{
    /// <summary>选择界面的提示文案（我们自己的表）。</summary>
    private const string PromptKey = "STS2_AEONGLASS_PROMPT_REWRITE";

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var prefs = new CardSelectorPrefs(new LocString("static_hover_tips", PromptKey), 1);
        var picked = await CardSelectCmd.FromHand(
            choiceContext, Owner, prefs, card => !ReferenceEquals(card, this), this);

        if (picked.FirstOrDefault() is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 复写：没有选中任何手牌（什么都不做）");
            return;
        }

        target.BaseReplayCount = 1;
        target.AddKeyword(CardKeyword.Retain);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 复写：{target.Id.Entry} 获得 重放 1 + 保留");
    }

}
