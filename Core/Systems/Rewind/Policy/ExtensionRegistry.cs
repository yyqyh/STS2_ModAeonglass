using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 扩展注册表。单个扩展抛异常只把它标为 Degraded 并记日志，其它扩展继续 —— 降级不中止。
/// </summary>
internal sealed class ExtensionRegistry(IRewindLog log)
{
    private readonly List<IRewindExtension> _items = [];
    private readonly HashSet<string> _degraded = [];

    public IReadOnlyList<IRewindExtension> Items => _items.OrderBy(e => e.Order).ToList();

    public IReadOnlyCollection<string> Degraded => _degraded;

    public void Add(IRewindExtension extension)
    {
        _items.RemoveAll(e => e.Name == extension.Name);
        _items.Add(extension);
    }

    public void Capture(ISnapshotDraft draft) => Run("capture", e => e.OnCapture(draft));

    public void BeforeRestore(RewindContext ctx) => Run("beforeRestore", e => e.OnBeforeRestore(ctx));

    public void AfterRestore(RewindContext ctx) => Run("afterRestore", e => e.OnAfterRestore(ctx));

    private void Run(string phase, Action<IRewindExtension> action)
    {
        foreach (var extension in Items)
        {
            try
            {
                action(extension);
                _degraded.Remove(extension.Name);
            }
            catch (Exception ex)
            {
                _degraded.Add(extension.Name);
                log.Warn($"扩展 {extension.Name} 在 {phase} 失败（已降级）：{ex.GetType().Name}");
            }
        }
    }
}
