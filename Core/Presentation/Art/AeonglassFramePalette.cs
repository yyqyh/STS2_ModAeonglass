using Godot;

namespace STS2_Aeonglass.Core.Presentation.Art;
/// <summary>卡框双色的<b>唯一色源</b>。</summary>
/// <remarks>
/// ① 职责：永时副 / 消逝副两套卡框色的唯一来源。
/// ② 调用时机：<see cref="AeonglassFrameMaterial" /> 建材质时读。
/// ③ 依赖：无（纯常量）。
/// ④ ⚠️ 调色<b>只改这个文件</b>。
/// </remarks>

internal static class AeonglassFramePalette
{
    /// <summary>shader 里"分割线以上"那个 uniform 名。</summary>
    public const string TopUniform = "color_top";

    /// <summary>shader 里"分割线以下"那个 uniform 名。</summary>
    public const string BottomUniform = "color_bottom";

    /// <summary><b>深蓝</b>（<c>#1E3F6B</c>）。</summary>
    public static readonly Color DeepBlue = new("1E3F6B");

    /// <summary><b>琥珀金</b>（<c>#D0952F</c>）。</summary>
    public static readonly Color AmberGold = new("D0952F");

    /// <summary>分割线以上用什么色。</summary>
    /// <param name="swapped">
    /// <c>true</c> = [消逝]（上下互换）；<c>false</c> = [永时]（本色：上蓝下金，也是图鉴默认）。
    /// </param>
    public static Color TopFor(bool swapped) => swapped ? AmberGold : DeepBlue;

    /// <summary>分割线以下用什么色（与 <see cref="TopFor" /> 严格互反）。</summary>
    /// <param name="swapped"><c>true</c> = [消逝]。</param>
    public static Color BottomFor(bool swapped) => swapped ? DeepBlue : AmberGold;
}
