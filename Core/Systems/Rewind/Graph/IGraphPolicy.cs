using System.Reflection;

namespace STS2_Aeonglass.Core.Systems.Rewind.Graph;

/// <summary>
/// 字段图策略契约：哪些算原子 / 保留引用 / 跳过。
/// </summary>
/// <remarks>
/// ① 职责：定义策略接口。
/// ② 调用时机：由 <c>FieldGraphCloner</c> 查询。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

public interface IGraphPolicy
{
    /// <summary>值语义类型：直接复制值，不再递归。</summary>
    bool IsAtomic(Type type);

    /// <summary>身份引用：拷贝时保留同一引用（模型、实体、节点等）。</summary>
    bool IsIdentityReference(object value);

    /// <summary>该字段不参与快照（静态 / 委托 / Task / 引擎节点等）。</summary>
    bool ShouldSkipField(FieldInfo field);

    /// <summary>特例克隆（例如 RNG 需要按种子重建）。返回 false 表示走通用流程。</summary>
    bool TryCloneSpecial(object value, object? owner, out object? copy);
}
