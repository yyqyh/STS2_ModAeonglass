using Godot;

namespace STS2_Aeonglass.Core.Presentation.Support;

/// <summary>表现层的<b>坐标/缩放</b>工具。</summary>
/// <remarks>
/// ① 职责：算"节点相对某个 UI 根的实际缩放"。
/// ② 调用时机：把卡节点交给本体飞行特效前（特效用的是 UI 空间坐标）。
/// ③ 依赖：Godot 画布变换（<c>GetGlobalTransformWithCanvas</c>）。
/// ④ ⚠️ 除以 UI 自己的缩放是<b>抵掉窗口拉伸</b>：否则画布缩放会被当成卡面尺寸，卡会明显变大。
/// </remarks>
internal static class UiGeometry
{
    /// <summary>卡节点相对战斗 UI 的累积缩放（= 手牌给 holder 的那个扇形缩放）。</summary>
    internal static Vector2 RelativeScale(CanvasItem node, CanvasItem ui)
    {
        var nodeScale = node.GetGlobalTransformWithCanvas().Scale;
        var uiScale = ui.GetGlobalTransformWithCanvas().Scale;
        return new Vector2(
            Mathf.IsZeroApprox(uiScale.X) ? 1f : nodeScale.X / uiScale.X,
            Mathf.IsZeroApprox(uiScale.Y) ? 1f : nodeScale.Y / uiScale.Y);
    }
}
