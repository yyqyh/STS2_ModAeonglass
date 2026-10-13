using System.Reflection;

namespace STS2_Aeonglass.Core.Systems.Rewind.Graph;

/// <summary>
/// 字段图策略：把"哪些类型算原子 / 哪些对象要保留引用 / 哪些字段要跳过 / 什么要特例克隆"
/// 从图引擎里抽出来。L1 因此可以完全不认识游戏类型，换游戏只换策略实现。
/// </summary>
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
