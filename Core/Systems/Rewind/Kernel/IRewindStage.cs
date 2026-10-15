namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 有序恢复阶段。加一个恢复项 = 加一个实现并注册，不改别的阶段。
/// </summary>
public interface IRewindStage
{
    string Name { get; }

    /// <summary>越小越先。</summary>
    int Order { get; }

    /// <summary>拍快照时收集本阶段需要的数据。</summary>
    void Capture(ISnapshotDraft draft);

    /// <summary>恢复本阶段负责的部分。失败由编排器统一回滚。</summary>
    void Restore(RewindContext ctx);
}
