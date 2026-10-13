using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 把 <see cref="IRewindExtension"/> 的 <c>OnBeforeRestore</c> / <c>OnAfterRestore</c> 真正挂到恢复流程上。
///
/// 为什么要包一层：RewindLab 原版只调了 <c>Extensions.Capture(draft)</c>，草稿随后被丢掉、
/// 两个恢复钩子从来没被调用。这里把"这份快照对应的草稿片段"随快照一起带着，
/// 恢复时先 <c>BeforeRestore</c>、再恢复本体、最后 <c>AfterRestore</c>（规格 §2.6.3 第 7 步）。
/// </summary>
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
