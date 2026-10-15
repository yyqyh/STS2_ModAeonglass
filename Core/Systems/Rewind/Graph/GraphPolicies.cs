using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace STS2_Aeonglass.Core.Systems.Rewind.Graph;

/// <summary>
/// 纯 BCL 的默认字段图策略：原子类型、委托/Task、容器。
/// </summary>
/// <remarks>
/// ① 职责：默认 <c>IGraphPolicy</c> 实现。
/// ② 调用时机：字段图抓取/恢复。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

public class GraphPolicies : IGraphPolicy
{
    /// <summary>这些命名空间前缀下的对象按引用保留（默认只保护引擎对象）。</summary>
    protected virtual bool IsPreservedNamespace(string fullName) =>
        fullName.StartsWith("Godot.", StringComparison.Ordinal);

    public virtual bool IsAtomic(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
        {
            return true;
        }

        return type == typeof(string)
            || type == typeof(Type)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid);
    }

    public virtual bool IsIdentityReference(object value)
    {
        var fullName = value.GetType().FullName ?? value.GetType().Name;
        return IsPreservedNamespace(fullName);
    }

    public virtual bool ShouldSkipField(FieldInfo field)
    {
        if (field.IsStatic)
        {
            return true;
        }

        var type = field.FieldType;
        if (typeof(Delegate).IsAssignableFrom(type)
            || typeof(Task).IsAssignableFrom(type)
            || typeof(CancellationTokenSource).IsAssignableFrom(type))
        {
            return true;
        }

        var fullName = type.FullName ?? type.Name;
        return fullName.StartsWith("System.Reflection", StringComparison.Ordinal);
    }

    public virtual bool TryCloneSpecial(object value, object? owner, out object? copy)
    {
        copy = null;
        return false;
    }
}
