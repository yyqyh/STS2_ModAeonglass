using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>倒仓</b>（NEW-35，2026-10-20）· <b>1 费 · Uncommon · 技能 · 消耗</b> ——
/// 抽 2 张，交换抽牌堆与弃牌堆，再次抽 1 张（升级 2）。
/// </summary>
/// <remarks>
/// ① 效果：两段抽牌之间做一次"抽/弃内容整体交换"（<c>PileSwapOnce</c>）。
/// ② 与乱流的区别：这是**一次性**交换；乱流是槽位对象长期对调。
/// ③ 空堆也照常完成前后两次抽牌（交换后抽牌堆可能为空 ⇒ 按本体洗牌兜底）。
/// ④ ⚠️ 交换是静默搬运 + 两次 <c>InvokeContentsChanged</c>，不经过 <c>CardAdded/CardRemoved</c>。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTurnover()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Skill(1, CardRarity.Uncommon)
            .Keyword(CardKeyword.Exhaust)
            .Var("Again", 1m, upgradeDelta: 1m))
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, 2, Owner);
        PileSwapOnce.Swap(Owner);
        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["Again"].BaseValue, Owner);
    }
}
