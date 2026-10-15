namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>快照标识</b>：序号 + 标签（日志与去重用）。
/// </summary>
/// <remarks>
/// ① 职责：标识一次快照。
/// ② 调用时机：抓取时生成。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public readonly record struct SnapshotId(int Index, int TurnNumber, DateTime Time, string Label)
{
    public override string ToString() => $"#{Index} T{TurnNumber} {Time:HH:mm:ss} {Label}";
}
