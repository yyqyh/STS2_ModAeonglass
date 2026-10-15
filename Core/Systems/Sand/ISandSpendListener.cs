using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Sand;

/// <summary>"消耗了沙砾"的监听口（例：每消耗至少 1 点沙砾 +2 挡）。</summary>
/// <remarks>
/// ① 职责：声明 <c>OnSandSpent(player, amount)</c>。
/// ② 挂点与时机：沙砾消耗只有一个写入口（<c>SandGapInvariant</c> 的支付订阅）⇒ 在那一处转发。
/// ③ 依赖：无。
/// ④ ⚠️ "每回合 1 次"这类闸门由实现方自己负责。
/// </remarks>
internal interface ISandSpendListener
{
    /// <param name="player">花沙砾的人。</param>
    /// <param name="amount">这一笔花了多少。</param>
    Task OnSandSpent(Player player, int amount);
}
