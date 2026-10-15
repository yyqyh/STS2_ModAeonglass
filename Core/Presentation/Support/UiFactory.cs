using Godot;

namespace STS2_Aeonglass.Core.Presentation.Support;

/// <summary>
/// 表现层共用的<b>控件小工厂</b>：Label / 扁平按钮。
/// </summary>
/// <remarks>
/// ① 职责：统一"建一个 Label/按钮 + 覆写字号与颜色"的写法（原先控制面板、意图卡带、坍缩按键各写一份）。
/// ② 调用时机：动态拼 UI 时（控制面板页、卡带的牌堆 popup、时空裂隙 按键）。
/// ③ 依赖：Godot <see cref="Label" /> / <see cref="Button" />；字体与主题仍由调用方按需覆写。
/// ④ ⚠️ 只做"共有的那部分覆写"：需要 Outline / 自定义 Font / 对齐 / MouseFilter 的（如 时空裂隙 按键文字），
///    走本工厂拿到 Label 之后再自己加。
/// </remarks>
internal static class UiFactory
{
    /// <summary>建一个 Label；<paramref name="color" /> 为空时不覆写颜色（用主题色）。</summary>
    internal static Label MakeLabel(string text, int fontSize, Color? color = null)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        if (color is { } c)
        {
            label.AddThemeColorOverride("font_color", c);
        }

        return label;
    }

    /// <summary>建一个"扁平按钮"（控制面板的按钮/页签就是这一种）。</summary>
    internal static Button MakeFlatButton(string text, Action onPressed, int fontSize = 11, bool expandFill = true)
    {
        var button = new Button { Text = text, Flat = true };
        if (expandFill)
        {
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }

        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.Pressed += onPressed;
        return button;
    }
}
