using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace STS2_Aeonglass.Core.Systems.Rewind.Graph;

/// <summary>字段级深拷贝 / 恢复：直接写私有字段 ⇒ <b>不触发事件、不走 setter</b>（规格 V1 的"静默恢复"）。</summary>
/// <remarks>
/// ① 职责：<c>Capture</c> / <c>TryRestore</c> / <c>GetHashCode</c>；克隆细节在 <c>FieldGraphCloner</c>。
/// ② 挂点与时机：<c>CombatStateSnapshot</c> 抓模型字段（power / 遗物）时逐字段用。
/// ③ 依赖：<c>IGraphPolicy</c>（哪些字段该抓）、字段缓存 <c>FieldCache</c>。
/// ④ ⚠️ 恢复是静默的 ⇒ 数字与通知不会自己刷新，恢复结束必须由 <c>RewindUiRefresh</c> 统一补刷。
/// </remarks>
public sealed class FieldGraphSnapshot
{
    internal static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static readonly Dictionary<Type, FieldInfo[]> FieldCache = [];

    private readonly object _root;
    private readonly Dictionary<FieldInfo, object?> _values = [];

    private FieldGraphSnapshot(object root, IGraphPolicy policy)
    {
        _root = root;
        var visited = new Dictionary<object, object?>(RefComparer.Instance);

        foreach (var field in FieldGraphCloner.EnumerateFields(root.GetType()))
        {
            if (policy.ShouldSkipField(field))
            {
                continue;
            }

            _values[field] = FieldGraphCloner.Clone(field.GetValue(root), root, policy, visited);
        }
    }

    public static FieldGraphSnapshot Capture(object root, IGraphPolicy policy) => new(root, policy);

    /// <summary>恢复。必须传同一个根实例，否则直接失败（安全阀）。</summary>
    public bool TryRestore(object root, IGraphPolicy policy, out string error)
    {
        if (!ReferenceEquals(root, _root))
        {
            error = $"根实例不一致：{_root.GetType().Name} → {root.GetType().Name}";
            return false;
        }

        var visited = new Dictionary<object, object?>(RefComparer.Instance);
        var failures = new List<string>();

        foreach (var (field, captured) in _values)
        {
            try
            {
                var current = field.GetValue(root);
                if (ReferenceEquals(current, captured))
                {
                    continue;
                }

                if (field.IsInitOnly && FieldGraphCloner.TryRestoreContainer(current, captured, policy, visited))
                {
                    continue;
                }

                field.SetValue(root, FieldGraphCloner.Clone(captured, root, policy, visited));
            }
            catch (Exception ex)
            {
                failures.Add($"{field.DeclaringType?.Name}.{field.Name}: {ex.GetType().Name}");
            }
        }

        error = failures.Count == 0 ? string.Empty : string.Join("; ", failures);
        return failures.Count == 0;
    }

    // ---------- 克隆 ----------

    // ---------- 只读容器：清空 + 回填（不替换容器本身） ----------

    // ---------- 基础设施 ----------

    private sealed class RefComparer : IEqualityComparer<object>
    {
        public static readonly RefComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
