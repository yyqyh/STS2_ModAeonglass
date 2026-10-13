using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>
/// 接缝侧的只读访问helper（读状态，不改状态）。
/// </summary>
/// <remarks>
/// Lab B 里这份代码叫 <c>LabA.Binding.GameAccess</c>；角色 mod 里只需要"枚举这一局的玩家"这一件事，
/// 所以收成一个小助手，不再单开一层 Binding。
/// </remarks>
internal static class BridgeAccess
{
    /// <summary>当前跑局（不在跑局里时为 null）。</summary>
    public static RunState? Run => RunManager.Instance.DebugOnlyGetState();

    /// <summary>这一局的全部玩家（单人 = 1 个）。</summary>
    public static IEnumerable<Player> Players() => Run?.Players ?? [];
}
