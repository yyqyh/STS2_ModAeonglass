namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>阶段枚举</b>：空闲 / 抓取中 / 恢复中（给状态机与 UI 读）。
/// </summary>
/// <remarks>
/// ① 职责：阶段取值。
/// ② 调用时机：<c>RewindState</c> 转换时。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public enum RewindPhase
{
    Idle,
    Locked,
    Restoring,
    Failed,
}
