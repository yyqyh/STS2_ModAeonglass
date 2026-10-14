using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.SandResource;

/// <summary>"消耗了沙砾"的监听口（例：SD-7 结壳 = 每消耗至少 1 点沙砾 +2 挡）。</summary>
/// <remarks>
/// 沙砾消耗只有一个写入口（支付沙砾的订阅：<c>AeonglassSandSubscription</c>）⇒ 在那一处转发。
/// 实现方自己负责"每回合 1 次"这类闸门。
/// </remarks>
internal interface IAeonglassSandSpendListener
{
    /// <param name="player">花沙砾的人。</param>
    /// <param name="amount">这一笔花了多少。</param>
    Task OnSandSpent(Player player, int amount);
}
