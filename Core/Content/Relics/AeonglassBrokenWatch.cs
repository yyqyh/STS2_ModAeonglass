using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;

using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Rewind;

namespace STS2_Aeonglass.Core.Content.Relics;

/// <summary>
/// 事件遗物：<b>破碎的怀表</b>（Event）—— <b>右键 = 回溯一次</b>（每场 1 次；成功后消耗全部沙砾）。
/// </summary>
/// <remarks>
/// ① 定位：回溯（回溯）的唯一玩家入口。获取方式 = 选完捏奥遗物之后<b>立刻出现的事件</b>（只有一个选项：获得本遗物）。
/// ② 触发时机：右键（必须走"排队动作"链路 <c>RewindModule.RequestFromQueuedAction</c>，而不是 <c>TryRequest</c> ——
///    右键是排队动作，队列没排空之前闸门会拦，实测踩过）；角标显示本场剩余次数（<c>剩余/1</c>，左上角）。
/// ③ 存档字段：本遗物自己没有 <c>[SavedProperty]</c>；"本场还剩几次"记在战斗级的 <c>RewindCombatLimit</c> 上，
///    "怀表事件待办"记在起始遗物的 <c>BrokenWatchPending</c> 上（随存档走）。
/// ④ 联动与口径：回溯（<c>RewindModule</c> / <c>RewindCombatLimit</c>）；事件链 <c>BrokenWatchChain</c> + 事件 <c>AeonglassBrokenWatchEvent</c>
///    （去重口径 = <b>每局一次</b>，按 (NetId, RunState) 记）；事件链落地前的<b>临时入口</b>是
///    <c>RewindModule.GrantTemporarilyIfMissing</c>（事件一落地就删掉那段）。
/// </remarks>
[RegisterRelic(typeof(AeonglassRelicPool))]
public sealed class AeonglassBrokenWatch : ModRelicTemplate, IModRightClickableRelic, IRelicExtraIconAmountLabelSpecsProvider
{
    /// <summary>事件遗物。</summary>
    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>图标 / 描边 / 大图都指向同一张空槽图（出图后同名覆盖）。</summary>
    public override RelicAssetProfile AssetProfile => new(
        IconPath: Const.Paths.Relic.BrokenWatch,
        IconOutlinePath: Const.Paths.Relic.BrokenWatch,
        BigIconPath: Const.Paths.Relic.BrokenWatch);

    /// <summary>本地快速筛选：只认我们这件遗物 + 本角色（其余情况不拦）。</summary>
    public bool CanHandleRightClickLocal(ModRightClickContext context) =>
        context.Model is AeonglassBrokenWatch && context.Player.Character is Aeonglass;

    /// <summary>同步判定：本角色 + 本场还没用过（更细的闸门交给 <see cref="RewindModule.TryRequest" /> 回话）。</summary>
    public bool CanExecuteRightClick(ModRightClickExecutionContext context) =>
        context.Player.Character is Aeonglass && RewindModule.CanRequest(context.Player);

    /// <summary>右键 = 回溯到上一回合（每场 1 次；成功后消耗全部沙砾）。</summary>
    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        var chain = context.Action is null ? "本地直调（多人下会不同步！）" : "同步动作";
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Rewind} 破碎的怀表右键触发回溯（链路={chain}）：已排队，等运行期稳定后发起");
        RewindModule.RequestFromQueuedAction(context.Player);

        return Task.CompletedTask;
    }

    /// <summary>角标：<b>左上 = 回溯</b>（本场还剩几次可用，<c>剩余/1</c>）。</summary>
    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        if (Owner is not { } player)
        {
            return [];
        }

        var rewindLeft = RewindCombatLimit.Used(player) ? 0 : 1;
        return
        [
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.TopLeft,
                $"[color=#7FE7DD]{rewindLeft}/1[/color]"),
        ];
    }
}
