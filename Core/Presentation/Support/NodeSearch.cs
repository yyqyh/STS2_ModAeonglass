using Godot;

namespace STS2_Aeonglass.Core.Presentation.Support;

/// <summary>
/// 表现层共用的<b>"找节点"</b>工具：深度优先找第一个满足条件的后代。
/// </summary>
/// <remarks>
/// ① 职责：把原先散在 6 个文件里的私有 DFS 查找（牌堆按钮 / 额外手牌容器 / 沙砾图标 …）收成一处。
/// ② 调用时机：挂载时、主动刷新时、动画前抓节点时。
/// ③ 依赖：Godot 树；目标节点类型由调用方的泛型 + 判定函数给出。
/// ④ ⚠️ <b>深度上限默认 6</b>（与各处原实现一致）——RitsuLib 现在把容器挂在 <c>NCombatUi</c> 下，
///    将来若多包一层容器，超过 6 层会<b>静默找不到</b>；改挂点结构时先回来核对这个常量。
/// </remarks>
internal static class NodeSearch
{
    /// <summary>默认深度上限（原实现一律是 6）。</summary>
    internal const int DefaultMaxDepth = 6;

    /// <summary>深度优先找第一个 <typeparamref name="T" />；可选判定函数，找不到返回 <c>null</c>。</summary>
    internal static T? FindDescendant<T>(Node? root, Func<T, bool>? predicate = null, int maxDepth = DefaultMaxDepth)
        where T : Node
    {
        var scanned = 0;
        return FindDescendant(root, predicate, maxDepth, ref scanned);
    }

    /// <summary>同上；另回报"扫过几个节点"（给会打日志的诊断用）。</summary>
    internal static T? FindDescendant<T>(Node? root, Func<T, bool>? predicate, int maxDepth, ref int scanned)
        where T : Node
    {
        if (root is null || maxDepth < 0)
        {
            return null;
        }

        foreach (var child in root.GetChildren())
        {
            scanned++;

            if (child is T hit && (predicate is null || predicate(hit)))
            {
                return hit;
            }

            if (FindDescendant(child, predicate, maxDepth - 1, ref scanned) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
