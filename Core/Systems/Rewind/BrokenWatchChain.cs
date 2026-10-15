using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Events;
using STS2_Aeonglass.Core.Content.Relics;

namespace STS2_Aeonglass.Core.Systems.Rewind;

/// <summary>
/// <b>「破碎的怀表」事件链</b>：把那个只有一个选项的事件，接着先古（捏奥）之后开出来。
/// </summary>
/// <remarks>
/// <para>
/// <b>三条触发路都要有</b>（用户 2026-10-14 报的漏就是第一条不够）：
/// ① 捏奥事件<b>刚结束</b> —— <c>EventRoom.OnEventStateChanged</c>（本体只在这条路上叫它）；
/// ② <b>在初始房间存档 / 读档</b>之后 —— 房间以"已完成（PreFinished）"恢复，本体<b>不会再叫</b>①那个钩子，
/// 所以必须补一个 <c>EventRoom.EnterInternal</c> 的入口；
/// ③ 离开初始房间之后才 SL（落在地图上）—— 由 <c>RoomEnteredEvent</c> 在地图上补接一次。
/// </para>
/// <para>
/// 待办状态记在起始遗物的 <c>[SavedProperty] BrokenWatchPending</c> 上 ⇒ 跨读档不丢；
/// 拿到怀表（或发现已经拿到了）就清零，所以"接事件"这件事最多发生一次。
/// </para>
/// </remarks>
internal static class BrokenWatchChain
{
    /// <summary>接链前的等待（秒）：让本体把"事件已结束、写档"这一拍走完。</summary>
    private const float ChainDelay = 0.4f;

    /// <summary>
    /// 已经接过的"玩家 + 这一局"（防重入 / 防连开两个事件房）。
    /// </summary>
    /// <remarks>
    /// <b>必须按 run 而不是按玩家记</b>（2026-10-15 修）：用户口径是"每局一次"，
    /// 而旧写法只按 <c>NetId</c> ⇒ 同一个进程里第一局接过之后，**之后每一局的新捏奥都被当成"已经接过"**
    /// （log：<c>事件链跳过…本进程里已经接过</c>）。<c>RunState</c> 每一局都是新对象 ⇒ 换局自动放行，
    /// 同局内仍然只接一次。
    /// </remarks>
    private static readonly HashSet<(ulong NetId, IRunState Run)> Chained = [];

    private static bool _inFlight;

    /// <summary>记一笔"还没办"（幂等；写在遗物上，随存档走）。</summary>
    public static void MarkPending(Player player)
    {
        if (RelicOf(player) is not { } relic || relic.BrokenWatchPending)
        {
            return;
        }

        relic.BrokenWatchPending = true;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 先古事件已结束，记下「破碎的怀表」待办（随存档走）");
    }

    /// <summary>清零（拿到怀表时调）。</summary>
    public static void Clear(Player player)
    {
        if (RelicOf(player) is not { BrokenWatchPending: true } relic)
        {
            return;
        }

        relic.BrokenWatchPending = false;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 「破碎的怀表」待办已清");
    }

    /// <summary>
    /// ① 现场那条：先古（捏奥）事件<b>刚走完</b>（钩子在 <c>EventRoom.OnEventStateChanged</c>）。
    /// </summary>
    public static void HandleAncientRoomLive(EventRoom room, string why)
    {
        if (room.LocalMutableEvent?.Owner is not { } owner)
        {
            return;
        }

        MarkPending(owner);
        ChainNow(owner, why);
    }

    /// <summary>
    /// ② 读档 / 重进那条：房间可能是"已完成（PreFinished）"状态回来的，这时本体的
    /// <c>OnEventStateChanged</c> <b>不会</b>再被调用 ⇒ 由 <c>RoomEnteredEvent</c> 补上一次。
    /// </summary>
    /// <remarks>玩家从生命周期事件里拿（读档那一刻本地可变事件还没就绪，别去读它）。</remarks>
    public static void HandleAncientRoomEntered(EventRoom room, IReadOnlyList<Player> players, string why)
    {
        if (!room.IsPreFinished && room.LocalMutableEvent is not { IsFinished: true })
        {
            return;
        }

        foreach (var player in players)
        {
            MarkPending(player);
            ChainNow(player, why);
        }
    }

    /// <summary>现在就接（①②两条路用它）。</summary>
    public static void ChainNow(Player player, string why)
    {
        if (!CanChain(player, out var blocker))
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 事件链跳过（{why}）：{blocker}");
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 接着开「破碎的怀表」事件（{why}）");
        TaskHelper.RunSafely(ChainAsync(player));
    }

    /// <summary>
    /// 落在地图上时"找机会补接"（③）。
    /// </summary>
    /// <remarks>只在<b>地图房</b>上补：战斗 / 别的事件房里硬开事件会打断本体的流程。</remarks>
    public static void TryChainAtRest(Player player, AbstractRoom room)
    {
        if (room.RoomType != RoomType.Map)
        {
            return;
        }

        if (player.Relics.OfType<AeonglassBrokenWatch>().Any())
        {
            Clear(player);
            return;
        }

        ChainNow(player, "读档后在地图上补接");
    }

    private static bool CanChain(Player player, out string blocker)
    {
        if (player.Character is not Aeonglass)
        {
            blocker = "不是沙漏角色";
            return false;
        }

        if (RelicOf(player) is not { BrokenWatchPending: true })
        {
            blocker = "没有待办标记";
            return false;
        }

        if (player.Relics.OfType<AeonglassBrokenWatch>().Any())
        {
            blocker = "已经有怀表";
            Clear(player);
            return false;
        }

        if (_inFlight || Chained.Contains((player.NetId, player.RunState)))
        {
            blocker = "本局里已经接过";
            return false;
        }

        blocker = string.Empty;
        return true;
    }

    private static async Task ChainAsync(Player player)
    {
        _inFlight = true;
        try
        {
            await Cmd.Wait(ChainDelay, ignoreCombatEnd: true);
            var room = new EventRoom(ModelDb.Event<AeonglassBrokenWatchEvent>());
            await RunManager.Instance.EnterRoomWithoutExitingCurrentRoom(room, fadeToBlack: true);
            Chained.Add((player.NetId, player.RunState));
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 「破碎的怀表」事件房间已进入");
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Rewind} 「破碎的怀表」事件开启失败：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _inFlight = false;
        }
    }

    private static AeonglassCore? RelicOf(Player player) => player.Relics.OfType<AeonglassCore>().FirstOrDefault();
}
