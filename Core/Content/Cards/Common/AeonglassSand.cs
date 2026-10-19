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
/// <b>沙</b>（CardNEW11，delta 20261015）· <b>1 费 · Common · 技能</b> —— 获得 2 沙砾（从空隙划）；抽 1 张牌（升级抽 2）。
/// </summary>
/// <remarks>
/// ① 效果：沙砾 +2 并把空隙 −2；升级只抬抽数。
/// ② 差分：无差分。
/// ③ 依赖：次级资源（<c>SecondaryResourceCmd</c>）+ 时之沙 恒等式 <c>沙砾 + 空隙 = 6</c>。
/// ④ ⚠️ 卡池侧标红过的<b>第二条「空隙 → 沙砾」回收路径</b>（用户 2026-10-15 决定要做）；实现成"从空隙划等量"故恒等式不破。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSand() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Common)
        .Var("Sand", SandBase)
        .Var("Draw", DrawBase, upgradeDelta: 1m))
{
    private const decimal SandBase = 2m;
    private const decimal DrawBase = 1m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var want = (int)DynamicVars["Sand"].BaseValue;
        var gap = SecondaryResourceCmd.Get(Owner, SandResourceRegistry.VoidId);
        var take = Math.Min(want, gap);

        if (take > 0)
        {
            await SecondaryResourceCmd.Gain(Owner, SandResourceRegistry.SandId, take, source: null);
            await SecondaryResourceCmd.Spend(Owner, SandResourceRegistry.VoidId, take);
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Sand} 沙：沙砾 +{take}（从空隙划；现有 沙砾 "
            + $"{SecondaryResourceCmd.Get(Owner, SandResourceRegistry.SandId)}"
            + $" / 空隙 {SecondaryResourceCmd.Get(Owner, SandResourceRegistry.VoidId)}）");

        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["Draw"].BaseValue, Owner);
    }

}
