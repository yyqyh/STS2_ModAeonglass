using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace STS2_Aeonglass.Core.Systems.Rewind.Graph;

/// <summary>
/// 字段图的<b>克隆器</b>：按策略深拷贝对象图（含容器重建与循环引用表）。
/// </summary>
/// <remarks>
/// ① 职责：<c>Clone</c> / <c>TryRestoreContainer</c> / <c>CreateContainer</c> / <c>EnumerateFields</c>。
/// ② 调用时机：<c>FieldGraphSnapshot.Capture/TryRestore</c> 内部。
/// ③ 依赖：<c>IGraphPolicy</c>（哪些算原子/保留引用/跳过）。
/// ④ ⚠️ 反射写私有字段 ⇒ 不触发任何事件；策略里别引入会分配的逻辑。
/// </remarks>
internal static class FieldGraphCloner
{

    internal static object? Clone(object? value, object? owner, IGraphPolicy policy, Dictionary<object, object?> visited)
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
            // ⚠️ `HashSet<T>` **不实现 `IList`**（2026-10-16 实测炸过：卡上的 `_keywords` 是
            //    `HashSet<CardKeyword>` ⇒ 这里的 `(IList)` 强转抛 InvalidCastException，
            //    而异常顺着 `CaptureTurnStart` 穿进本体 `CombatManager.StartTurn` ⇒
            //    日志 "turn loop died … the combat is stuck"，战斗卡死、回合结束按键不出现）。
            //    ⇒ 只能按 `Add(T)` 反射逐个加（方法句柄缓存，见 <see cref="AddMethod" />）。
            var copy = CreateContainer(type, typeof(HashSet<>))!;
            visited[value] = copy;
            var add = AddMethod(type);
            foreach (var item in (IEnumerable)value)
            {
                add?.Invoke(copy, [Clone(item, owner, policy, visited)]);
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
        var clone = FieldGraphSnapshot.MemberwiseCloneMethod.Invoke(value, null)!;
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

    internal static bool TryRestoreContainer(
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
            ClearMethod(type)?.Invoke(destination, null);
            var add = AddMethod(type);
            foreach (var item in (IEnumerable)source)
            {
                add?.Invoke(destination, [Clone(item, null, policy, visited)]);
            }

            return true;
        }

        return false;
    }

    internal static bool IsGenericHashSet(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>);

    private static readonly Dictionary<Type, MethodInfo?> AddCache = [];

    private static readonly Dictionary<Type, MethodInfo?> ClearCache = [];

    /// <summary>`HashSet&lt;T&gt;.Add(T)` 的句柄（集合不是 `IList`，只能反射加元素；句柄缓存）。</summary>
    private static MethodInfo? AddMethod(Type type)
    {
        if (AddCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var element = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
        var add = type.GetMethod("Add", [element]);
        AddCache[type] = add;
        return add;
    }

    /// <summary>`HashSet&lt;T&gt;.Clear()` 的句柄（缓存，别每帧去查）。</summary>
    private static MethodInfo? ClearMethod(Type type)
    {
        if (ClearCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var clear = type.GetMethod("Clear");
        ClearCache[type] = clear;
        return clear;
    }

    internal static object? CreateContainer(Type type, Type openGeneric)
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

    internal static IEnumerable<FieldInfo> EnumerateFields(Type type)
    {
        if (FieldGraphSnapshot.FieldCache.TryGetValue(type, out var cached))
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
        FieldGraphSnapshot.FieldCache[type] = array;
        return array;
    }
}
