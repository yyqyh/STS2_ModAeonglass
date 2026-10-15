using Godot;

using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace STS2_Aeonglass.Core.Presentation.Support;

/// <summary>表现层的<b>等待</b>工具：以一个节点为宿主跑一段纯等待。</summary>
/// <remarks>
/// ① 职责：统一"用 <c>Tween.Interval</c> 等 N 秒"的写法（原来散在动画流水线里）。
/// ② 调用时机：动画分段之间、让本体的移动/特效走完的那一拍。
/// ③ 依赖：宿主节点的 <c>SceneTree</c>（宿主被释放 ⇒ await 直接结束，不会吊住流程）。
/// ④ ⚠️ 宿主必须是**在树里**的节点，否则拿不到 Tween。
/// </remarks>
internal static class GodotWait
{
    /// <summary>等 <paramref name="seconds" /> 秒（宿主被释放时提前结束）。</summary>
    internal static async Task SecondsAsync(Node owner, float seconds)
    {
        var tween = owner.CreateTween();
        tween.TweenInterval(seconds);
        await tween.AwaitFinished(owner);
    }
}
