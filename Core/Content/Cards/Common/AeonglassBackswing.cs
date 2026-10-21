using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Gap;
using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>回摆</b>（CardINV1，v2 + delta 20261015）· <b>1 沙砾 · Common · 技能</b> —— 获得 5 点格挡（升级 7）；<b>沙砾变换过</b>：额外获得 3 点格挡。
/// </summary>
/// <remarks>
/// ① 效果：基础 5 挡；升级只抬基础档（+2），额外那 3 点不变。
/// ② 差分：无差分（"沙砾变换过"是资源条件，不是副差分）。
/// ③ 依赖：<c>SandTurnCounters.SandChangedThisTurn</c>（本回合沙砾值变动过 ⇒ 翻转 / 消耗 / 获得都算；记账口是次级资源变动钩子 <c>AfterSecondaryResourceChanged</c>）。
/// ④ ⚠️ delta 把条件从"每翻转一次"放宽成"沙砾变换过" ⇒ 触发面大得多。
/// ⑤ 2026-10-20：改为<b>沙砾费卡</b>（0 能量 + 1 沙砾），费用画在卡面星辉同位；
///    实现 <see cref="ISandCostCard" /> 供间隙换回补挂费用。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBackswing : AeonglassCardTemplate, ISandCostCard
{
    public AeonglassBackswing()
        : base(
            AeonglassCardSpec.Block(0, CardRarity.Common, 5m, 2m)
                .Var("Bonus", BonusIfSandChanged)
                .Effect(CardEffects.If(
                    "sand-changed",
                    context => SandTurnCounters.SandChangedThisTurn(context.Player),
                    CardEffects.GainBlockFixed(BonusIfSandChanged))))
    {
        if (SandResourceRegistry.Sand is { } sand)
        {
            this.SecondaryCosts().Set(sand.Id, SandCost);
        }
    }

    /// <summary>"沙砾变换过"那一档的额外格挡（升级不变）。</summary>
    private const decimal BonusIfSandChanged = 3m;

    /// <summary>打出这张牌要花 1 点沙砾（0 能量）。</summary>
    public int SandCost => 1;

    /// <summary>已放在 <c>images/cards/</c> 的卡面。</summary>
    public override CardAssetProfile AssetProfile =>
        new(PortraitPath: Const.Paths.CardArt.Backswing);
}
