using System.Collections.Generic;

using HarmonyLib;

using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Content.Cards;

namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// 【测试期临时设施】把静默猎手的牌并进永世沙漏卡池（逻辑见 <see cref="SilentCardPoolBorrow" />）。
/// </summary>
/// <remarks>
/// <para>
/// 挂在本体 <c>CardPoolModel.AllCards</c> 的 getter 后置：只有 <c>__instance</c> 是我们自己的
/// <c>AeonglassCardPool</c>、且设置页的开关开着时才追加，其它卡池（含本体静默猎手）完全不受影响。
/// </para>
/// <para>
/// <b>为什么用 patch 而不是 <c>ModHelper.AddModelToPool</c></b>：本体在 <c>ModelDb.Init</c> 之前就跑完
/// 模组的 ModInitializer（<c>OneTimeInitialization.ExecuteVeryEarly</c> → <c>ExecuteEssential</c> →
/// <c>ModelDb.Init</c>），那时既枚举不到本体卡实例；而 <c>AddModelToPool</c> 又要求"必须在卡池被冻结前"调用，
/// 顺序很脆。挂在 getter 后置则是惰性的：第一次真正取卡池内容时才算，天然不依赖启动顺序。
/// </para>
/// <para><b>补丁绝不让异常外泄</b>：任何异常都吞掉并打日志，避免一个调试设施带崩所有卡池。</para>
/// </remarks>
[HarmonyPatch(typeof(CardPoolModel), "get_AllCards")]
internal static class SilentCardPoolBorrowPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        try
        {
            if (SilentCardPoolBorrow.ShouldApply(__instance))
            {
                __result = SilentCardPoolBorrow.Merge(__instance, __result);
            }
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] [调试] 卡池借用失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
