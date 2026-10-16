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

    /// <summary>
    /// 把 <paramref name="child" /> 等比缩到 <paramref name="box" /> 里并**实测居中**，返回图标在盒子本地坐标里的矩形。
    /// </summary>
    /// <param name="boxMinSize">
    /// 盒子**将要**占的尺寸（<c>CustomMinimumSize</c>）。用传进来的值而不是 <c>box.Size</c>：
    /// 容器还没排版时 <c>box.Size</c> 是 0，用它算出来的中心是错的。
    /// </param>
    /// <remarks>
    /// ⚠️ **不能**写 <c>child.Position = (boxMinSize - size * scale) / 2</c>：本体的卡牌/牌堆按钮自己带
    /// <c>PivotOffset</c>（按下、悬停要绕中心缩），Godot 绕轴心缩放 ⇒ 位置会被顶出去
    /// <c>pivot × (1 - scale)</c>。所以这里缩放完**量一次**（子节点屏幕中心 vs 盒子屏幕中心）再把差补回去，
    /// 拿到的是"看得见的那个矩形"，标签就照它摆。
    /// </remarks>
    internal static Rect2 FitAndCenterIn(Control child, Control box, Vector2 boxMinSize)
    {
        var raw = child.Size;
        if (raw.X <= 1f || raw.Y <= 1f || boxMinSize.X <= 1f || boxMinSize.Y <= 1f)
        {
            return new Rect2(Vector2.Zero, boxMinSize);
        }

        var scale = Mathf.Min(boxMinSize.X / raw.X, boxMinSize.Y / raw.Y);
        child.Scale = new Vector2(scale, scale);

        var boxXform = box.GetGlobalTransformWithCanvas();
        var childXform = child.GetGlobalTransformWithCanvas();
        var delta = (boxXform * (boxMinSize * 0.5f)) - (childXform * (raw * 0.5f));
        var boxScale = boxXform.Scale;
        if (!Mathf.IsZeroApprox(boxScale.X) && !Mathf.IsZeroApprox(boxScale.Y))
        {
            child.Position += new Vector2(delta.X / boxScale.X, delta.Y / boxScale.Y);
        }

        var iconSize = raw * scale;
        return new Rect2(boxMinSize * 0.5f - iconSize * 0.5f, iconSize);
    }
}
