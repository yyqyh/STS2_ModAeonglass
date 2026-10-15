using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 回溯的 UI 探针：把"界面现在什么样"变成可读文本（只读）。
/// </summary>
/// <remarks>
/// ① 职责：取手牌/牌堆/计数器读数。
/// ② 调用时机：调试与控制面板。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class RewindUiProbe
{
    internal static bool IsInCardSelection
    {
        get
        {
            try
            {
                return NPlayerHand.Instance?.IsInCardSelection ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static int OverlayScreenCount
    {
        get
        {
            try
            {
                return NOverlayStack.Instance?.ScreenCount ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    internal static bool IsTargeting
    {
        get
        {
            try
            {
                return NTargetManager.Instance?.IsInSelection ?? false;
            }
            catch
            {
                return false;
            }
        }
    }
}
