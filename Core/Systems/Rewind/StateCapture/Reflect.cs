using System.Collections;
using System.Reflection;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 私有字段访问的单一入口：带缓存、永不抛。
/// 反射失败只返回 false，避免一个失效字段把整场战斗带崩。
/// </summary>
internal static class Reflect
{
    private const BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Dictionary<(Type, string), FieldInfo?> FieldCache = [];

    private static readonly Dictionary<(Type, string), MethodInfo?> MethodCache = [];

    internal static FieldInfo? Field(Type type, string name)
    {
        var key = (type, name);
        if (FieldCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        FieldInfo? found = null;
        for (var current = type; current is not null; current = current.BaseType)
        {
            found = current.GetField(name, Flags);
            if (found is not null)
            {
                break;
            }
        }

        FieldCache[key] = found;
        return found;
    }

    internal static bool TryGet<T>(object? owner, string name, out T value)
    {
        value = default!;
        if (owner is null)
        {
            return false;
        }

        var raw = Field(owner.GetType(), name)?.GetValue(owner);
        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }

    internal static T? Get<T>(object? owner, string name) where T : class =>
        TryGet<T>(owner, name, out var value) ? value : null;

    internal static bool Set(object? owner, string name, object? value)
    {
        if (owner is null)
        {
            return false;
        }

        var field = Field(owner.GetType(), name);
        if (field is null)
        {
            return false;
        }

        try
        {
            field.SetValue(owner, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>集合字段的元素个数；取不到按 0 处理（"空"）。</summary>
    internal static int Count(object? owner, string name) =>
        TryGet<ICollection>(owner, name, out var collection) ? collection.Count : 0;

    /// <summary>字典字段里的 int 值是否全为 0（"效果深度为 0"）。取不到按 true 处理。</summary>
    internal static bool AllZeroValues(object? owner, string name)
    {
        if (!TryGet<IDictionary>(owner, name, out var dictionary))
        {
            return true;
        }

        foreach (var value in dictionary.Values)
        {
            if (value is int i && i != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>按名字调一个实例方法（可带参数），签名不匹配或失败都只返回 false。</summary>
    internal static bool Invoke(object? owner, string name, params object?[] args)
    {
        if (owner is null)
        {
            return false;
        }

        var key = (owner.GetType(), name);
        if (!MethodCache.TryGetValue(key, out var method))
        {
            method = null;
            for (var current = owner.GetType(); current is not null; current = current.BaseType)
            {
                var candidates = current
                    .GetMethods(Flags)
                    .Where(m => m.Name == name && m.GetParameters().Length == args.Length)
                    .ToArray();
                if (candidates.Length > 0)
                {
                    method = candidates[0];
                    break;
                }
            }

            MethodCache[key] = method;
        }

        if (method is null)
        {
            return false;
        }

        try
        {
            method.Invoke(owner, args);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>写属性（含 private setter），让 setter 里的事件照常触发。</summary>
    internal static bool SetProperty(object? owner, string name, object? value)
    {
        if (owner is null)
        {
            return false;
        }

        for (var current = owner.GetType(); current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(
                name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (property?.SetMethod is not { } setter)
            {
                continue;
            }

            try
            {
                setter.Invoke(owner, [value]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
}
