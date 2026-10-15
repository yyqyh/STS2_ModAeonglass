using Godot;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;

using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;
using STS2_Aeonglass.Core.Systems.Rewind.Bridges;
using STS2_Aeonglass.Core.Systems.Rewind.Host;
using STS2_Aeonglass.Core.Systems.Rewind.Host.Patches;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;
using STS2_Aeonglass.Core.Systems.Sand;

using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Rewind;

/// <summary>
/// 回溯的<b>代价</b>：成功后消耗全部沙砾。
/// </summary>
/// <remarks>
/// ① 职责：<c>SpendAllSandAsync</c>（把当前沙砾一次花光，触发"花沙砾生空隙"不变式）。
/// ② 调用时机：<c>RewindModule</c> 的回溯成功之后。
/// ③ 依赖：时之沙的次级资源支付口。
/// ④ ⚠️ 顺序不能反：先成功再扣费；已花光时这一步是 0 沙砾。
/// </remarks>
internal static class RewindCost
{

    /// <summary>收取回溯代价：把此刻的沙砾全部扣掉（定档；设置里可关）。</summary>
    internal static async Task SpendAllSandAsync(Player player)
    {
        try
        {
            var amount = Math.Max(0, SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId));
            if (amount == 0)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 回溯代价：本来就没有沙砾，跳过。");
                return;
            }

            var spent = await SecondaryResourceCmd.Spend(player, SandResourceRegistry.SandId, amount);
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Rewind} 回溯代价：消耗 {amount} 沙砾（成功={spent}）"
                + $" → 现在 {RewindModule.Describe(player)}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Rewind} 收取回溯代价时抛异常：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
