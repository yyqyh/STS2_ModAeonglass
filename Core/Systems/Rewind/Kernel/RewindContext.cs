namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>阶段间共享的上下文。除 <see cref="Bag"/> 外都由编排器填写。</summary>
public sealed class RewindContext(ISnapshot target, IRewindLog log)
{
    public ISnapshot Target { get; } = target;

    public IRewindLog Log { get; } = log;

    /// <summary>阶段/扩展之间的临时数据（例如"这次回档新建了哪些对象"）。</summary>
    public Dictionary<string, object?> Bag { get; } = [];

    /// <summary>
    /// 这次快照拍下来时各扩展写进 <see cref="ISnapshotDraft"/> 的片段（只读）。
    /// <c>OnBeforeRestore</c> / <c>OnAfterRestore</c> 从这里取回"当时"的状态 ——
    /// 扩展不能用实例字段缓存，否则会被回档事务里的"备份快照"覆盖。
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parts { get; init; } = new Dictionary<string, object?>();
}
