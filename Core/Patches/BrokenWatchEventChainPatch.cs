using HarmonyLib;

using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Relics;
using STS2_Aeonglass.Core.Systems.Rewind;

namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// <b>「破碎的怀表」事件链</b>：捏奥（第一幕先古）那个事件房间一结束，就把我们的事件房间接在它后面。
/// </summary>
/// <remarks>
/// <para>
/// <b>两个钩子缺一不可</b>：
/// <list type="bullet">
///   <item><description><c>OnEventStateChanged</c> —— 事件"刚走完"那一拍（本体在里面 <c>MarkPreFinished</c> + 写档）；</description></item>
///   <item><description><c>EnterInternal</c> —— <b>在初始房间存档 / 读档</b>时房间以"已完成"状态恢复，
///   本体<b>不会</b>再叫前一个钩子（它只在 <c>!IsPreFinished</c> 时调）⇒ 只靠前一个会漏掉这条，
///   表现就是"初始房间 SL 之后事件不再出现"（用户 2026-10-14 实测）。</description></item>
/// </list>
/// 真正开房间的动作在 <see cref="BrokenWatchChain" />（用本体的
/// <c>EnterRoomWithoutExitingCurrentRoom</c>，即它自己"接着开下一个房间"的同款入口）。
/// </para>
/// <para>
/// 条件与去重都在 <see cref="BrokenWatchChain" /> 里（角色门 + 待办标记 + 有没有怀表 + 本进程去重）；
/// 这里只负责"认出这是捏奥那个房间、且它已经走完"。异常只记日志，绝不把本体的事件流程带崩。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(EventRoom), "OnEventStateChanged")]
internal static class BrokenWatchEventChainPatch
{
    [HarmonyPostfix]
    private static void Postfix(EventRoom __instance)
    {
        AfterAncientRoom(__instance, "捏奥事件刚结束");
    }

    /// <summary>给"房间进入"那条路（含读档恢复）复用：房间已是捏奥 + 已走完 ⇒ 记待办并接链。</summary>
    private static void AfterAncientRoom(EventRoom room, string why)
    {
        try
        {
            if (room.CanonicalEvent is not Neow)
            {
                return;
            }

            BrokenWatchChain.HandleAncientRoomLive(room, $"{why}｜房间已完成={room.IsPreFinished}");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Rewind} 事件链接入失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
