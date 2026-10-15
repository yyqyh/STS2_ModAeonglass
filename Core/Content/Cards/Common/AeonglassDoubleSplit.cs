using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>双分</b>（CardPH3，v2）· <b>0 费 · Common · 技能</b> —— 获得 3 点格挡（升级 5）并抽 1 张牌；
/// <b>永时副</b>：若沙砾＝3，改为抽 2 张；<b>消逝副</b>：若本回合你消耗过沙砾，改为抽 2 张。
/// </summary>
/// <remarks>
/// ① 效果：先格挡（走差分格挡模板），再按当前副的条件决定抽 1 张还是 2 张；升级只抬格挡（+2）。
/// ② 差分：永时副（A）看<b>相位</b>（沙砾＝3 平衡档）；消逝副（B）看<b>本回合有没有花过沙砾</b>。
/// ③ 依赖：相位读口 <c>SandPhases.IsBalanced</c> + 回合内沙砾记账 <c>SandTurnCounters.SpentSandThisTurn</c>。
/// ④ ⚠️ 卡池侧按新定价公式（<c>aeonglass_pricing_formula_v2.md</c>）把本卡列入<b>亏模</b>清单，数值等用户拍上调幅度后再改。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDoubleSplit() : AeonglassVariantBlockTemplate(0, CardRarity.Common, 3m, 2m)
{
    /// <summary>格挡之后抽几张：基础 1，条件命中 +1。</summary>
    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hit = IsVariantB
            ? SandTurnCounters.SpentSandThisTurn(Owner)
            : SandPhases.IsBalanced(Owner);
        var draw = hit ? 2 : 1;

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Sand} 双分（{(IsVariantB ? "消逝副" : "永时副")}）："
            + $"{(IsVariantB ? "本回合消耗过沙砾" : $"沙砾 {SecondaryResourceCmd.Get(Owner, SandResourceRegistry.SandId)}")}"
            + $" ⇒ 抽 {draw}");

        await CardPileCmd.Draw(choiceContext, draw, Owner);
    }
}
