using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Presentation.Character;
using STS2_Aeonglass.Core.Systems.Bootstrap;


namespace STS2_Aeonglass.Core.Presentation.Combat.Gap;

/// <summary>
/// 时空裂隙 战斗按键的<b>文案与调参常量</b>（按钮本体见 <see cref="AeonglassGapActionRow" />）。
/// </summary>
/// <remarks>
/// ① 职责：按钮文字 / 悬停说明 / 相对能量计数器的位置与尺寸 —— <b>要挪按钮只改这个文件</b>。
/// ② 挂点与时机：由 <see cref="AeonglassGapActionRowHost" /> 在战斗房间就绪时使用；文案随"当前合法动作"切换。
/// ③ 依赖：无（纯常量）；位置基准是战斗能量计数器节点。
/// ④ ⚠️ 口径：2026-10-13/14 用户定"<b>一个</b>按钮、文案跟着合法动作走"（坍缩/唤回必须交替 ⇒ 同时最多一个合法）、
///    "每回合 1 次"的额度已取消 ⇒ 显示哪个动作由<b>间隙里有没有牌</b>决定。
///    待办（补按钮图标 / 按下特效 / 可用态特效）见 <c>Core/Presentation/README.md</c>。
/// </remarks>
internal static class AeonglassGapActionTexts
{
    /// <summary>按钮文字（用户指定）。</summary>
    internal const string CollapseText = "坍缩";

    /// <summary>「坍缩」的悬停说明（用户指定）。</summary>
    internal const string CollapseTip = "坍缩：将手牌置于时空裂隙";

    /// <summary>按钮文字（用户指定）。</summary>
    internal const string RecallText = "唤回";

    /// <summary>「唤回」的悬停说明（用户指定）。</summary>
    internal const string RecallTip = "唤回：用时空裂隙中的牌替换手牌";

    /// <summary>
    /// 相对能量计数器底边的垂直偏移（px，负值 = 往上）。
    /// </summary>
    /// <remarks>
    /// 用户 2026-10-13 第一次口径"正下方"，第二次"再往上 40px" ⇒ 10 - 40 = -30（那时按钮高 38）。
    /// <b>2026-10-13 换成月牙后按钮高 128</b>，为了让"按钮中心"还停在原来那个位置，再减掉半个高度差
    /// （(128-38)/2 = 45）⇒ -75。想再挪只改这一个数。
    /// </remarks>
    internal const float GapBelowCounter = -90f;

    /// <summary>相对能量计数器左缘的水平偏移（px）。</summary>
    internal const float XOffset = 30f;

    /// <summary>
    /// 按钮尺寸。<b>2026-10-13 换成月牙（128×128）</b>：原版底板那条 132×38 的横条不要了。
    /// </summary>
    internal static readonly Vector2 ButtonSize = new(128f, 128f);

    /// <summary>
    /// 文字在月牙"肚子"里的落点（128 画布坐标）。
    /// </summary>
    /// <remarks>
    /// 月牙几何 = 外圆 A 挖掉切圆 B，两圆交点是小角；<b>圆 A 圆心落在圆 B 内部</b>，
    /// 所以实心臂在<b>右下</b>（美术侧已整体左右翻转定死这个方向）。
    /// 圆 A 圆心 = (64,67)（256 画布 (128,134) 的一半），实心臂中点约在它沿"背向 B"方向 20px 处 ⇒ 这里取 (78,86)。
    /// 觉得偏了就只改这个常数。
    /// </remarks>
    internal static readonly Vector2 TextCenter = new(78f, 86f);

    /// <summary>文字所占矩形（相对 <see cref="TextCenter" /> 居中）。</summary>
    internal static readonly Vector2 TextSize = new(66f, 30f);
}
