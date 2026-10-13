using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace STS2_Aeonglass.Core.Systems.Rewind.Graph;

/// <summary>
/// 字段级深拷贝 / 恢复。恢复时直接写私有字段 ⇒ <b>不触发任何事件、不经过任何 setter</b>。
/// 这是"静默恢复"的基础（规格 V1）。
/// </summary>
public sealed class FieldGraphSnapshot
{
    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly Dictionary<Type, FieldInfo[]> FieldCache = [];

    private readonly object _root;
    private readonly Dictionary<FieldInfo, object?> _values = [];

    private FieldGraphSnapshot(object root, IGraphPolicy policy)
    {
        _root = root;
        var visited = new Dictionary<object, object?>(RefComparer.Instance);

        foreach (var field in EnumerateFields(root.GetType()))
        {
            if (policy.ShouldSkipField(field))
            {
                continue;
            }

            _values[field] = Clone(field.GetValue(root), root, policy, visited);
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

                if (field.IsInitOnly && TryRestoreContainer(current, captured, policy, visited))
                {
                    continue;
                }

                field.SetValue(root, Clone(captured, root, policy, visited));
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

    private static object? Clone(object? value, object? owner, IGraphPolicy policy, Dictionary<object, object?> visited)
    {
        if (value is null)
        {
            return null;
        }

        var type = value.GetType();
        if (policy.IsAtomic(type) || policy.IsIdentityReference(value))
        {
            return value;
        }

        if (visited.TryGetValue(value, out var made))
        {
            return made;
        }

        if (policy.TryCloneSpecial(value, owner, out var special))
        {
            visited[value] = special!;
            return special;
        }

        if (type.IsArray)
        {
            var source = (Array)value;
            var copy = (Array)Activator.CreateInstance(type, source.Length)!;
            visited[value] = copy;
            for (var i = 0; i < source.Length; i++)
            {
                copy.SetValue(Clone(source.GetValue(i), owner, policy, visited), i);
            }

            return copy;
        }

        if (value is IDictionary dictionary)
        {
            var copy = (IDictionary)CreateContainer(type, typeof(Dictionary<,>))!;
            visited[value] = copy;
            foreach (DictionaryEntry entry in dictionary)
            {
                copy[Clone(entry.Key, owner, policy, visited)!] =
                    Clone(entry.Value, owner, policy, visited);
            }

            return copy;
        }

        if (IsGenericHashSet(type))
        {
            var copy = (IList)CreateContainer(type, typeof(HashSet<>))!;
            visited[value] = copy;
            foreach (var item in (IEnumerable)value)
            {
                copy.Add(Clone(item, owner, policy, visited));
            }

            return copy;
        }

        if (value is IList list)
        {
            var copy = (IList)CreateContainer(type, typeof(List<>))!;
            visited[value] = copy;
            foreach (var item in list)
            {
                copy.Add(Clone(item, owner, policy, visited));
            }

            return copy;
        }

        // 普通对象：浅拷贝后逐字段克隆
        var clone = MemberwiseCloneMethod.Invoke(value, null)!;
        visited[value] = clone;
        foreach (var field in EnumerateFields(type))
        {
            if (policy.ShouldSkipField(field))
            {
                continue;
            }

            var cloned = Clone(field.GetValue(value), owner, policy, visited);
            try
            {
                field.SetValue(clone, cloned);
            }
            catch
            {
                TryRestoreContainer(field.GetValue(clone), cloned, policy, visited);
            }
        }

        return clone;
    }

    // ---------- 只读容器：清空 + 回填（不替换容器本身） ----------

    private static bool TryRestoreContainer(
        object? destination, object? source, IGraphPolicy policy, Dictionary<object, object?> visited)
    {
        if (destination is null || source is null)
        {
            return false;
        }

        if (destination is IDictionary dictionary && source is IDictionary sourceDictionary)
        {
            dictionary.Clear();
            foreach (DictionaryEntry entry in sourceDictionary)
            {
                dictionary[Clone(entry.Key, null, policy, visited)!] = Clone(entry.Value, null, policy, visited);
            }

            return true;
        }

        if (destination is IList list && source is IList sourceList)
        {
            list.Clear();
            foreach (var item in sourceList)
            {
                list.Add(Clone(item, null, policy, visited));
            }

            return true;
        }

        var type = destination.GetType();
        if (IsGenericHashSet(type) && source.GetType() == type)
        {
            type.GetMethod("Clear")?.Invoke(destination, null);
            var add = type.GetMethod("Add");
            foreach (var item in (IEnumerable)source)
            {
                add?.Invoke(destination, [Clone(item, null, policy, visited)]);
            }

            return true;
        }

        return false;
    }

    // ---------- 基础设施 ----------

    private static bool IsGenericHashSet(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>);

    private static object? CreateContainer(Type type, Type openGeneric)
    {
        if (!type.IsInterface && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null)
        {
            return Activator.CreateInstance(type);
        }

        var arguments = type.IsGenericType
            ? type.GetGenericArguments()
            : openGeneric == typeof(Dictionary<,>)
                ? [typeof(object), typeof(object)]
                : [typeof(object)];

        return Activator.CreateInstance(openGeneric.MakeGenericType(arguments));
    }

    private static IEnumerable<FieldInfo> EnumerateFields(Type type)
    {
        if (FieldCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var fields = new List<FieldInfo>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            fields.AddRange(current.GetFields(
                BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        var array = fields.ToArray();
        FieldCache[type] = array;
        return array;
    }

    private sealed class RefComparer : IEqualityComparer<object>
    {
        public static readonly RefComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
