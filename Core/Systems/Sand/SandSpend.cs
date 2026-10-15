using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Combat.SecondaryResources;

namespace STS2_Aeonglass.Core.Systems.Sand;

/// <summary>「可消耗 N 沙砾改为…」的判定入口（SD 通道的卡共用）。</summary>
/// <remarks>
/// ① 职责：<c>CanAfford</c> 够不够付；<c>TrySpendAsync</c> 够就付掉并返回 true、不够返回 false。
/// ② 挂点与时机：卡牌效果结算前调用。
/// ③ 依赖：<c>SecondaryResourceCmd</c> + <c>SandResourceRegistry</c> 的沙砾。
/// ④ ⚠️ 卡表写的是"<b>可</b>消耗"，这里实现成<b>够就花（自动）</b>（StS 一贯的"有就花"，还免一次确认弹窗）；
/// 若要改成玩家可选，只动 <c>TrySpendAsync</c> 一处。走 <c>Spend</c> 而不是 <c>Lose</c> ⇒ 会触发"支付沙砾 → 生成等量空隙"，
/// 消耗沙砾会把相位推向空隙侧（正是 SD 通道的设计意图）。
/// </remarks>
internal static class SandSpend
{
    /// <summary>够不够付。</summary>
    public static bool CanAfford(Player player, int amount)
        => amount <= 0 || SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId) >= amount;

    /// <summary>够就付掉并返回 true；不够返回 false（调用方按基础效果结算）。</summary>
    public static async Task<bool> TrySpendAsync(Player player, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        if (!CanAfford(player, amount))
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Sand} 沙砾不足（需要 {amount}）⇒ 按基础效果结算");
            return false;
        }

        var paid = await SecondaryResourceCmd.Spend(player, SandResourceRegistry.SandId, amount);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Sand} 消耗 {amount} 沙砾 ⇒ 强化本次效果（success={paid}）");
        return paid;
    }
}
