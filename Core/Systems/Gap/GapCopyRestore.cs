using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>
/// 时空裂隙「间隙 → 手牌」时的**费用补挂单点**。
/// </summary>
/// <remarks>
/// <para>
/// <b>现在这条路上正常情况下不会丢费用</b>：间隙里放的是 <c>CombatState.CloneCard</c> 造出来的
/// **真实实例**（深拷贝，附魔 / 升级 / `[SavedProperty]` / 次级资源费用全都保留），
/// 不像"序列化成纯数据再还原"那条路会丢东西。
/// </para>
/// <para>
/// 那为什么还留着它：**跨存档**那条路仍然要过序列化（牌堆随 run 存档）。
/// 如果真的从存档回来后费用没了，这里是唯一能补回来的地方 —— 按卡的规范声明
/// （<see cref="ISandCostCard" />）重新挂一遍，成本 3 行，属于保险。
/// </para>
/// </remarks>
internal static class GapCopyRestore
{
    /// <summary>按卡的规范声明重新挂上沙砾费（幂等）。</summary>
    public static void ReapplyCosts(CardModel card)
    {
        if (card is not ISandCostCard costCard || costCard.SandCost <= 0)
        {
            return;
        }

        if (SandResourceRegistry.Sand is not { } sand)
        {
            return;
        }

        card.SecondaryCosts().Set(sand.Id, costCard.SandCost);
    }
}
