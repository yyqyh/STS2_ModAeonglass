using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Combat.SecondaryResources;

namespace STS2_Aeonglass.Core.Systems.SandResource;

/// <summary>「可消耗 N 沙砾改为…」的判定入口（SD 通道的卡共用）。</summary>
/// <remarks>
/// <para>
/// <b>口径</b>：卡表写的是"**可**消耗" —— 目前实现成 **够就花（自动）**：够就付、不够就按基础效果结算。
/// 理由：StS 里"花资源换更强效果"的一贯处理是"有就花"，而且免去一次额外的确认弹窗；
/// 若卡池侧要的是"玩家可选"，改成弹一次确认即可（只动这一个方法）。
/// </para>
/// <para>
/// 走 <c>Spend</c>（不是 <c>Lose</c>）⇒ 会触发 M1 的"支付沙砾 → 生成等量空隙"订阅，
/// 于是消耗沙砾会把相位推向空隙侧 —— 这正是 SD 通道的设计意图。
/// </para>
/// </remarks>
internal static class SandSpend
{
    /// <summary>够不够付。</summary>
    public static bool CanAfford(Player player, int amount)
        => amount <= 0 || SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId) >= amount;

    /// <summary>够就付掉并返回 true；不够返回 false（调用方按基础效果结算）。</summary>
    public static async Task<bool> TrySpendAsync(Player player, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (!CanAfford(player, amount))
        {
            Const.Logger.Info($"[{Const.ModId}] 沙砾不足（需要 {amount}）⇒ 按基础效果结算");
            return false;
        }

        var paid = await SecondaryResourceCmd.Spend(player, AeonglassSecondaryResources.SandId, amount);
        Const.Logger.Info($"[{Const.ModId}] 消耗 {amount} 沙砾 ⇒ 强化本次效果（success={paid}）");
        return paid;
    }
}
