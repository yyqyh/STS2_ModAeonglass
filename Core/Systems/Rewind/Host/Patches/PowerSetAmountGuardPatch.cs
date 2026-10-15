using HarmonyLib;

using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.Host.Patches;

/// <summary>恢复期拦掉 <c>PowerModel.SetAmount</c>，避免回档过程触发副作用（规格 §2.6.3 的坑 1）。</summary>
/// <remarks>
/// ① 职责：只在 <c>RestoreGuard.IsRestoring</c> 期间拦 —— 直接把私有 <c>_amount</c> 写掉并返回 false。
/// ② 挂点与时机：Harmony 前缀补丁，本体每次设置能力数值时跑；非恢复期直接放行。
/// ③ 依赖：<c>RestoreGuard</c>、<c>Coordinates.PowerModel.Amount</c>、<c>Reflect.Set</c>、<c>RewindUiRefresh</c>。
/// ④ ⚠️ <c>SetAmount(n, silent: true)</c> 并不完全静默：仍会发 <c>DisplayAmountChanged</c>（<c>NPower</c> 刷数字）与
/// <c>Owner.InvokePowerModified</c>（<c>PowerIncreased</c>/<c>PowerDecreased</c> 的唯一事件源，<c>CombatStateTracker</c> 与联机都订阅）
/// ⇒ 这条补丁一次覆盖两条路径；恢复结束由 <c>RewindUiRefresh</c> 统一补刷一次数字与通知。
/// </remarks>
[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]

internal static class PowerSetAmountGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PowerModel __instance, int amount)
    {
        try
        {
            if (!RestoreGuard.IsRestoring)
            {
                return true;
            }

            Reflect.Set(__instance, Coordinates.PowerModel.Amount, Math.Clamp(amount, -999999999, 999999999));
            return false;
        }
        catch (Exception ex)
        {
            // 兜底：反射失败时放行本体行为（宁可恢复期多几次通知，也不要打断本体的能力数值设置）。
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Rewind} 恢复期拦 SetAmount 失败（放行本体）：{ex.GetType().Name}: {ex.Message}");
            return true;
        }
    }
}
