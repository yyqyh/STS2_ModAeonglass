using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 把扩展的<b>片段</b>并入快照：恢复时先还原扩展数据，再走本体字段图。
/// </summary>
/// <remarks>
/// ① 职责：包装快照 + 调扩展回调。
/// ② 调用时机：抓取/恢复。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

internal sealed class ExtensionAwareSnapshot(
    ISnapshot inner,
    ExtensionRegistry extensions,
    IReadOnlyDictionary<string, object?> parts) : ISnapshot
{
    public SnapshotId Id => inner.Id;

    public string Summary => inner.Summary;

    public bool TryRestore(IRewindLog log, out string error)
    {
        var ctx = new RewindContext(this, log) { Parts = parts };

        extensions.BeforeRestore(ctx);
        var ok = inner.TryRestore(log, out error);
        if (ok)
        {
            extensions.AfterRestore(ctx);
        }

        return ok;
    }

    public void Release() => inner.Release();
}
