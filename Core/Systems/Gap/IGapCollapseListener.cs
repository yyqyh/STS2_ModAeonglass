using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>
/// 「刚坍缩完」的监听口（GRW 通道拿它做"坍缩时额外抽 1 张"这类收益）。
/// </summary>
/// <remarks>
/// 坍缩只有一个入口（<c>GapCollapseActions.Activate</c>）⇒ 通知就在那一处的成功路径上发，不做钩子、不做补丁。
/// 口径与 搁浅的搁浅通知一致：实现方自己负责"每回合 1 次"这类闸门。
/// </remarks>
internal interface IGapCollapseListener
{
    /// <param name="player">动作归属玩家。</param>
    /// <param name="copies">这次坍缩进去几张副本。</param>
    Task OnCollapsed(Player player, int copies);
}
