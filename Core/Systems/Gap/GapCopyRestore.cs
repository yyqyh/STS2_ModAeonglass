using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>时空裂隙「间隙 → 手牌」时的<b>费用补挂单点</b>。</summary>
/// <remarks>
/// ① 职责：换回时按卡的规范声明（<see cref="ISandCostCard" />）重新挂上沙砾费用。
/// ② 挂点与时机：<c>GapRecallActions</c> 把副本搬回手牌的那一步。
/// ③ 依赖：<c>ISandCostCard</c>、RitsuLib 的次级资源费用 API。
/// ④ ⚠️ 正常路上不丢费用（间隙里是 <c>CombatState.CloneCard</c> 深拷贝出来的真实实例）；留着它是替<b>跨存档</b>那条路兜底
/// （牌堆随 run 存档 ⇒ 要过序列化），成本 3 行。
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
