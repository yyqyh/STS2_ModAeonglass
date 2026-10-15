namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 回溯的<b>回合级作用域</b>：标记"本次回档要回退的回合数据"。
/// </summary>
/// <remarks>
/// ① 职责：回合作用域的开关。
/// ② 调用时机：回合粒度抓取/恢复。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

internal sealed class TurnScope
{
    /// <summary>回合开始打点（默认开）。</summary>
    public bool CaptureOnTurnStart { get; init; } = true;

    /// <summary>每个动作结束后打点（默认关，调试用）。</summary>
    public bool CaptureAfterEveryAction { get; init; }

    public static TurnScope Default { get; } = new();
}
