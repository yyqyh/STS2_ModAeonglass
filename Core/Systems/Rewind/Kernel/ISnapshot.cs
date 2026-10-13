namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 一次快照。引擎只认识这个接口，不认识它到底存了什么：具体内容由 L2/L3 决定。
/// </summary>
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
