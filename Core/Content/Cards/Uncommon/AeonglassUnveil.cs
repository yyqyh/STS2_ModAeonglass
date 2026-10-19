using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>揭幕</b>（NEW-16）· <b>1 费 · Uncommon · 技能</b> —— 打出抽牌堆顶的一张牌，并赋予其「迟滞」。
/// </summary>
/// <remarks>
/// <para>
/// ① 效果：顶牌先打上「迟滞」标记，再按本体的"从抽牌堆自动打出"流程打掉它 ⇒ 它落进弃牌堆时带着迟滞
/// （之后随机的洗牌会跳过它 —— 正是「迟滞」的口径：只挡随机洗牌、不挡主动捞回）。
/// </para>
/// <para>
/// ② 实现照抄本体 <c>CardPileCmd.AutoPlayFromDrawPile</c> 的两步（洗一次 → 取出顶牌 → 搬进 Play 堆 → 自动打出），
/// 只是把"取出"和"打标"之间插进我们自己的标记 —— 用本体那个入口就没地方插标记了。
/// ⚠️ 顶 = <c>Cards[0]</c>（本体 <c>MoveToTopInternal</c> 是 <c>Insert(0,…)</c>、<c>AutoPlayFromDrawPile</c> 取 <c>FirstOrDefault()</c>）。
/// </para>
/// <para>
/// ③ 依赖：<c>Hysteresis.Mark</c>（迟滞标记表）+ 本体自动打出流程。<b>不</b>依赖乱流（乱流只包 <c>CardPileCmd</c> 那两个入口，这里是直接调 <c>CardCmd.AutoPlay</c>）。
/// </para>
/// <para>
/// ④ ⚠️ 已知显示限制（RitsuLib 关键词槽位）：运行时打的迟滞标记**机制一定生效**，但卡面不一定出得来「迟滞」词条
/// （见 <c>HysteresisKeyword</c> 的注释）。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassUnveil() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Uncommon)
        .UpgradeEnergy(-1))
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState is not { } piles)
        {
            return;
        }

        // ⚠️ 读的是**属性**（乱流生效时它已被对调）⇒ 玩家看到的"抽牌堆"就是这里取到的这张堆。
        var draw = piles.DrawPile;

        // 第一步与本体 AutoPlayFromDrawPile 一致：抽牌堆空了就先洗回一次（否则下面没有顶牌可取）。
        if (draw.Cards.Count == 0)
        {
            await CardPileCmd.ShuffleIfNecessary(choiceContext, Owner);
        }

        if (draw.Cards.FirstOrDefault() is not { } top)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 揭幕：抽牌堆是空的（洗过也没有）⇒ 只拿 0 张");
            return;
        }

        // 先打标：接下来被搬走、被自动打出的就是这一张（同一个实例）⇒ 迟滞跟着它进弃牌堆。
        Hysteresis.Mark(top);

        await CardPileCmd.Add(top, PileType.Play);
        await CardCmd.AutoPlay(choiceContext, top, null);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 揭幕：抽牌堆顶 {top.Id.Entry} 已打上迟滞并自动打出");
    }

}
