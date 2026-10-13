using HarmonyLib;

using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Systems;

namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// 演出期间**拒绝"结束回合"**（用户 2026-10-12：「回溯、坍缩等操作都要加屏障」）。
/// </summary>
/// <remarks>
/// <para>
/// 不挡的话：坍缩 / 回归那 1～2 秒里点结束回合，手牌会在动画中途被弃掉、我们那几张还在飞的卡
/// 会落进"已经结束的回合"里，画面和状态就错位了。本体的卡牌 VFX 是排队等玩家动作的，
/// 而我们的演出不在那条队列上，所以必须自己挡。
/// </para>
/// <para>
/// 只挡<see cref="OperationBarrier.IsBusy">屏障</see>为真的那几帧 —— 屏障只有我们自己的演出会进，
/// 所以原版 / 其他 mod 的角色行为完全不变（<c>IsBusy</c> 恒为 false，Prefix 直接放行）。
/// </para>
/// </remarks>
internal static class OperationBarrierPatch
{
    /// <summary>是不是该把这次"结束回合"按掉（并把原因写进日志）。</summary>
    private static bool ShouldBlock()
    {
        if (!OperationBarrier.IsBusy)
        {
            return false;
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Gap} 演出进行中（{OperationBarrier.Describe()}）⇒ 本次「结束回合」已被拒绝");
        return true;
    }

    /// <summary>按住/点击那条路。</summary>
    [HarmonyPatch(typeof(NEndTurnButton), "OnPress")]
    internal static class OnPress
    {
        [HarmonyPrefix]
        private static bool Prefix() => !ShouldBlock();
    }

    /// <summary>真正执行"我准备好了"那条路（长按释放 / 快捷键也走这里）。</summary>
    [HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.CallReleaseLogic))]
    internal static class ReleaseLogic
    {
        [HarmonyPrefix]
        private static bool Prefix() => !ShouldBlock();
    }
}
