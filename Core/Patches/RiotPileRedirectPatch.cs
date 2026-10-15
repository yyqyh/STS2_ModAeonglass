using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Systems.Riot;

namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// <b>乱流 · 兜底收口</b>：字段方案不可用时，把"按类型取堆"的结果替换成对面那张。
/// </summary>
/// <remarks>
/// <para>
/// <b>主方案是写字段</b>（见 <see cref="RiotSwap" />）：直接交换 <c>PlayerCombatState</c> 上
/// <c>&lt;DrawPile&gt;k__BackingField</c> / <c>&lt;DiscardPile&gt;k__BackingField</c>。
/// 原因：这两个属性是 3 条 IL 的自动属性 getter，JIT 会把它们<b>内联</b>进
/// <c>CardPile.Get</c> / <c>PileTypeExtensions.GetPile</c> / <c>CardPileCmd.DrawInternal</c> 等调用点，
/// Harmony 改不到那些内联副本 ⇒ 只挂 getter 时本体抽/弃/洗一条都不过补丁（已实测）。
/// </para>
/// <para>
/// <b>这里只挂两个属性 getter，而且绝不和字段方案同时生效</b>：
/// <c>RiotSwap.Redirect</c> 在字段方案可用时直接原样返回（字段已经兜住，再换一次就是换回来）。
/// 另外注意<b>不要再挂 <c>CardPile.Get</c></b>：它内部读的就是这两个属性，叠加必抵消。
/// </para>
/// <para>
/// ⚠️ <b>范围与兜底（2026-10-15 补）</b>：这两个 getter 是<b>全角色、全时刻</b>都在读的热路径 ⇒
/// ① 第一行先用 <c>RiotSwap.HasPair</c> 短路（乱流没在场的局立刻返回，连 <c>Redirect</c> 都不进）；
/// ② 再包一层 try/catch —— <c>Redirect</c> 里会做反射写字段，一旦失败<b>保持本体原堆</b>并记日志，
/// 绝不让异常从"取抽/弃牌堆"这条路穿进本体。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.DrawPile), MethodType.Getter)]
internal static class RiotDrawPileRedirectPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerCombatState __instance, ref CardPile __result)
        => Redirect(__instance, ref __result);

    /// <summary>共用的收口：短路 + 兜底（弃牌堆那一半同款，所以是 internal）。</summary>
    internal static void Redirect(PlayerCombatState state, ref CardPile pile)
    {
        if (!RiotSwap.HasPair(state))
        {
            return;
        }

        try
        {
            pile = RiotSwap.Redirect(state, pile);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Riot} 堆重定向失败（保持本体原堆）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>同上：弃牌堆那一半。</summary>
[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.DiscardPile), MethodType.Getter)]
internal static class RiotDiscardPileRedirectPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerCombatState __instance, ref CardPile __result)
    {
        // 与抽牌堆那一半完全同款：先短路、再走共用的"兜底 + 重定向"。
        if (!RiotSwap.HasPair(__instance))
        {
            return;
        }

        RiotDrawPileRedirectPatch.Redirect(__instance, ref __result);
    }
}
