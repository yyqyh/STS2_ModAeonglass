using Godot;

namespace STS2_Aeonglass.Core.Presentation.Art;

/// <summary>UI 观感色的**唯一色源**（卡框那套在 <see cref="AeonglassFramePalette" />）。</summary>
/// <remarks>
/// ① 职责：沙漏青 / 发牌琥珀金 / 红闪 的唯一来源。
/// ② 调用时机：卡面高光、发牌特效、传送门漩涡建材质/粒子时读。
/// ③ 依赖：无（纯常量）。
/// ④ ⚠️ 调色**只改这个文件**（原来是"卡面一份 + 传送门一份"，两处容易改漏）。
/// </remarks>

internal static class AeonglassUiPalette
{
    /// <summary><b>沙漏青</b>（<c>#7FE7DD</c>）：选中高光、传送门染色、漩涡共用。</summary>
    public static readonly Color Cyan = new("7FE7DD");

    /// <summary>发牌扫描带的**热端**琥珀金（靠近发射器）。</summary>
    public static readonly Color DealAmberHot = new("FFD98A");

    /// <summary>发牌扫描带的**冷端**琥珀金（往左飘远的那一头）。</summary>
    public static readonly Color DealAmberCold = new("FFB347");

    /// <summary>发牌卡框红闪的红色（半透明）。</summary>
    public static readonly Color FlashRed = new(1f, 0.22f, 0.22f, 0.95f);
}
