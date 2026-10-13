using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding;

/// <summary>UI 侧的"现在有没有东西在等玩家操作"，与逻辑侧的 <see cref="CombatAccess.IsRuntimeSettled"/> 互补。</summary>
internal static class UiProbe
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
