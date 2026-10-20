using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>触顶</b>（NEW-31，2026-10-20）· <b>1 费 · Uncommon · 技能</b> · <b>迟滞</b> ——
/// 本牌在抽牌堆顶时立刻进入手牌；从弃牌堆抽 3 张（升级 4）。
/// </summary>
/// <remarks>
/// ① 效果：打出时从弃牌堆抽 N 张；"在抽牌堆顶 ⇒ 回手"由 <c>DrawTopWatcher</c> 在卡外触发。
/// ② 触发时点：抽牌堆的 <c>CardAdded</c> / <c>CardRemoved</c> 之后延迟一小拍检查堆顶
///    （原文"若本牌在抽牌堆顶前"按"位于堆顶时"记录，交接单授权由落实方接现有抽牌管线）。
/// ③ 依赖：<c>DiscardDraw</c>（从弃牌堆抽）+ <c>DrawTopWatcher</c>（卡外触发）+ 迟滞关键词。
/// ④ ⚠️ 从弃牌堆抽按"弃牌堆堆顶顺序取前 N 张"；手牌满/不足都按实际张数截断。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTouchTop()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Skill(1, CardRarity.Uncommon)
            .Keyword(HysteresisKeyword.Value)
            .Var("Draw", 3m, upgradeDelta: 1m)),
    IHysteresisCard
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var count = (int)DynamicVars["Draw"].BaseValue;
        await DiscardDraw.DrawAsync(choiceContext, Owner, count);
    }
}
