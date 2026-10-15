namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>一次快照</b>：引擎只认这个接口，内容由各层自己填。
/// </summary>
/// <remarks>
/// ① 职责：<c>TryRestore</c> / <c>Release</c> 的最小契约。
/// ② 调用时机：抓取时产生、恢复时消费。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public interface ISnapshot
{
    SnapshotId Id { get; }

    /// <summary>一行摘要（面板列表用），例如 <c>cards 12 powers 5 hist 41</c>。</summary>
    string Summary { get; }

    /// <summary>恢复到该快照。返回 false 时调用方必须回滚，不允许半恢复。</summary>
    bool TryRestore(IRewindLog log, out string error);

    /// <summary>释放大对象；被环形缓冲裁剪时调用。</summary>
    void Release();
}
