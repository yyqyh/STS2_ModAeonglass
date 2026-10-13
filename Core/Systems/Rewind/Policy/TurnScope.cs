namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>打点策略：默认每个玩家回合开始拍一份；单步模式是调试用的额外打点。</summary>
internal sealed class TurnScope
{
    /// <summary>回合开始打点（默认开）。</summary>
    public bool CaptureOnTurnStart { get; init; } = true;

    /// <summary>每个动作结束后打点（默认关，调试用）。</summary>
    public bool CaptureAfterEveryAction { get; init; }

    public static TurnScope Default { get; } = new();
}
