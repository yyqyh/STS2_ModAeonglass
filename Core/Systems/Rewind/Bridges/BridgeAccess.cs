using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>
/// 接缝侧的只读访问助手（读状态，不改状态）。
/// </summary>
/// <remarks>
/// ① 职责：给各接缝共享的读取口。
/// ② 调用时机：接缝回调。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class BridgeAccess
{
    /// <summary>当前跑局（不在跑局里时为 null）。</summary>
    public static RunState? Run => RunManager.Instance.DebugOnlyGetState();

    /// <summary>这一局的全部玩家（单人 = 1 个）。</summary>
    public static IEnumerable<Player> Players() => Run?.Players ?? [];
}
