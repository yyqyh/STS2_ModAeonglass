using Godot;

namespace STS2_Aeonglass.Core.Presentation.Support;

/// <summary>表现层的<b>节点存活判定</b>：非空 + Godot 对象有效 + 还在树里。</summary>
/// <remarks>
/// ① 职责：动画/异步流程里判"这个卡节点还能不能用"（三件事必须同时成立，单判 null 会漏）。
/// ② 调用时机：await 之后的每一步（本体的飞行特效会中途收掉节点）。
/// ③ 依赖：<c>GodotObject.IsInstanceValid</c> + <c>Node.IsInsideTree</c>。
/// ④ ⚠️ C# 侧持有的是**托管引用**：节点被 <c>QueueFree</c> 后引用还在，所以别用 <c>!= null</c> 判断。
/// </remarks>
internal static class NodeLifetime
{
    /// <summary>这个节点现在还能安全用吗。</summary>
    internal static bool IsAlive(Node? node)
        => node is not null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();
}
