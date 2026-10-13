using HarmonyLib;

using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Rewind.Binding;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.Host.Patches;

/// <summary>
/// 恢复期拦掉 <c>PowerModel.SetAmount</c>（规格 §2.6.3 恢复期的坑 1）。
///
/// <c>SetAmount(n, silent: true)</c> <b>并不完全静默</b>：它仍会发
/// <c>DisplayAmountChanged</c>（<c>NPower</c> 订阅 ⇒ 刷新数字）与 <c>Owner.InvokePowerModified</c>
/// （<c>PowerIncreased</c> / <c>PowerDecreased</c> 的唯一事件源头，<c>CombatStateTracker</c> 与联机节点都订阅）。
///
/// 这里在恢复期直接把 <c>_amount</c> 写掉并返回 false，一次覆盖两条路径；恢复结束由
/// <c>UiRefresh</c> 统一刷一次数字与通知。
/// </summary>
[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
internal static class PowerSetAmountGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(PowerModel __instance, int amount)
    {
        if (!RestoreGuard.IsRestoring)
        {
            return true;
        }

        Reflect.Set(__instance, Coordinates.PowerModel.Amount, Math.Clamp(amount, -999999999, 999999999));
        return false;
    }
}
