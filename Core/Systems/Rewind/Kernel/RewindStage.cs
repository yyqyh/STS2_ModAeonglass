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

/// <summary>快照草稿：各阶段往这里塞自己的片段，最后合成一个 <see cref="ISnapshot"/>。</summary>
public interface ISnapshotDraft
{
    SnapshotId Id { get; }

    void Put(string key, object? value);

    object? Get(string key);
}

/// <summary>默认草稿实现。</summary>
public sealed class SnapshotDraft(SnapshotId id) : ISnapshotDraft
{
    private readonly Dictionary<string, object?> _parts = [];

    public SnapshotId Id { get; } = id;

    public IReadOnlyDictionary<string, object?> Parts => _parts;

    public void Put(string key, object? value) => _parts[key] = value;

    public object? Get(string key) => _parts.GetValueOrDefault(key);
}
