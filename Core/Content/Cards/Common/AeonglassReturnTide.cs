using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>返潮</b>（NEW-17）· <b>1 费 · Common · 技能</b> —— 获得 6 点格挡；把弃牌堆里随机一张带「迟滞」的牌放回抽牌堆顶。
/// </summary>
/// <remarks>
/// <para>
/// ① 效果：格挡走底线件模板；额外效果 = 在弃牌堆里筛「迟滞」牌，随机（<c>RunState.Rng.CombatCardSelection</c>，
/// 与本体"随机抽牌堆顶"同一路流 ⇒ 可复现、也被回溯快照覆盖）挑一张放回<b>抽牌堆顶</b>（<c>CardPilePosition.Top</c>）。
/// </para>
/// <para>
/// ② 迟滞标记**不因这次捞回而清掉**（用户 2026-10-16 确认）：迟滞的既有口径就是"只挡随机洗牌、不挡主动捞回"，
/// 所以它被捞上来之后再洗一次还是会留在弃牌堆。
/// </para>
/// <para>
/// ③ 弃牌堆里没有迟滞牌 ⇒ 只拿格挡、写一行日志（不是失败）。
/// </para>
/// <para>
/// ④ 卡面词条：格挡件模板给了 <c>CardTag.Defend</c>（"下一张防御牌 +N"类效果认它），与「防御 / 堤岸」同口径。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassReturnTide() : AeonglassCardTemplate(
    AeonglassCardSpec.Block(1, CardRarity.Common, 6m, 2m))
{
    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState is not { } piles)
        {
            return;
        }

        // ⚠️ 同揭幕：读属性（乱流生效时它已被对调），"弃牌堆"以玩家看到的为准。
        var candidates = piles.DiscardPile.Cards.Where(Hysteresis.IsStuck).ToList();
        if (candidates.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 返潮：弃牌堆里没有带迟滞的牌 ⇒ 只拿格挡");
            return;
        }

        var pick = candidates[Owner.RunState.Rng.CombatCardSelection.NextInt(candidates.Count)];
        var result = await CardPileCmd.Add(pick, PileType.Draw, CardPilePosition.Top, null, skipVisuals: false);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 返潮：弃牌堆 {candidates.Count} 张迟滞牌里随机捞 {pick.Id.Entry} → 抽牌堆顶"
            + $"（成功={result.success}）");
    }
}
