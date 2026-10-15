using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 回溯的<b>扩展注册表</b>：按 order 排序调度，单个扩展抛异常只标记它 Degraded。
/// </summary>
/// <remarks>
/// ① 职责：注册 / 排序 / 逐阶段派发。
/// ② 调用时机：模块装配时注册；每次回溯派发。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

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
