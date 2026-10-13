namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>快照标识。值语义，可安全跨层传；<see cref="Index"/> 只在同一个 <see cref="SnapshotRing"/> 内有意义。</summary>
public readonly record struct SnapshotId(int Index, int TurnNumber, DateTime Time, string Label)
{
    public override string ToString() => $"#{Index} T{TurnNumber} {Time:HH:mm:ss} {Label}";
}
