using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 回溯的<b>次级资源桥</b>：把沙砾/空隙这类 run 级资源纳入回档（默认实现为空）。
/// </summary>
/// <remarks>
/// ① 职责：<c>ISecondaryResourceBridge</c>（接口）+ <c>NullSecondaryResourceBridge</c>（未接入占位）。
/// ② 调用时机：抓取/恢复资源读数时。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

internal interface ISecondaryResourceBridge
{
    /// <summary>是否已接上真实实现。false 时快照/恢复都跳过。</summary>
    bool Available { get; }

    IReadOnlyDictionary<string, int> Capture(Player player);

    void Restore(Player player, IReadOnlyDictionary<string, int> snapshot);
}
