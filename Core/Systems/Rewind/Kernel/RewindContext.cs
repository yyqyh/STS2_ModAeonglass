namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>上下文</b>：把快照草稿与本次事务信息交给扩展。
/// </summary>
/// <remarks>
/// ① 职责：扩展拿到的只读上下文。
/// ② 调用时机：每次抓取 / 恢复回调。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

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
