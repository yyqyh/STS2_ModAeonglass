using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Relics;

namespace STS2_Aeonglass.Core.Content.Events;

/// <summary>
/// <b>事件「破碎的怀表」</b>：只有一个选项 —— 获得事件遗物<b>破碎的怀表</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>2026-10-14 用户口径</b>：这个事件要<b>另开</b>，并且出现在<b>捏奥（第一幕先古）之后</b>。
/// 承接点在 <c>Core/Patches/BrokenWatchEventChainPatch.cs</c>：捏奥那个事件房间一结束，
/// 就把本事件的房间<b>叠在它上面</b>（<c>EnterRoomWithoutExitingCurrentRoom</c>）。
/// 玩家看到的是：捏奥 → 淡出 → 这个只有一个选项的事件 → 收下怀表 → 回地图。
/// </para>
/// <para>
/// <b>不进随机池</b>：<see cref="IsAllowed" /> 恒 false，所以本体抽随机事件那句
/// <c>NextEvent.IsAllowed(runState)</c> 永远不会挑中它；只有上面那条链会开它。
/// </para>
/// <para>
/// 文案键（事件 id = <c>STS2_AEONGLASS_EVENT_AEONGLASS_BROKEN_WATCH_EVENT</c>，见 <c>localization/zhs/events.json</c>）：
/// <c>&lt;id&gt;.title</c> / <c>&lt;id&gt;.pages.INITIAL.description</c> /
/// <c>&lt;id&gt;.pages.INITIAL.options.TAKE.title|.description</c> / <c>&lt;id&gt;.pages.DONE.description</c>。
/// </para>
/// </remarks>
[RegisterSharedEvent]
public sealed class AeonglassBrokenWatchEvent : ModEventTemplate
{
    /// <summary>
    /// 立绘：显式指向我们的空槽图。
    /// </summary>
    /// <remarks>
    /// <b>这一条不是美术洁癖，是"必须有"</b>：不给的话本体会按事件 id 去推
    /// <c>res://images/events/&lt;id 小写&gt;.png</c>，文件不存在 ⇒ <c>CreateInitialPortrait</c> 抛
    /// <c>AssetLoadException</c> ⇒ 事件布局初始化中断、**一个选项都不显示**（2026-10-14 实测）。
    /// </remarks>
    public override EventAssetProfile AssetProfile =>
        new(InitialPortraitPath: Const.Paths.Event.BrokenWatchPortrait);

    /// <summary>只有我们那条链会开它，所以本体的随机抽选永远不选它。</summary>
    public override bool IsAllowed(IRunState runState) => false;

    /// <summary>唯一的选项：收下怀表。</summary>
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var owner = Owner ?? throw new InvalidOperationException(
            $"[{Const.ModId}] 事件 {Id.Entry} 在拿到归属玩家之前就生成了选项");

        var relic = ModelDb.Relic<AeonglassBrokenWatch>().ToMutable();
        relic.AssertMutable();
        relic.Owner = owner;

        return
        [
            EventOption.FromRelic(relic, this, () => TakeAsync(relic, owner), ModOptionKey("INITIAL", "TAKE")),
        ];
    }

    private async Task TakeAsync(RelicModel relic, Player owner)
    {
        await RelicCmd.Obtain(relic, owner);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 事件「破碎的怀表」：已获得该遗物");
        SetEventFinished(L10NLookup(Id.Entry + ".pages.DONE.description"));
    }
}
