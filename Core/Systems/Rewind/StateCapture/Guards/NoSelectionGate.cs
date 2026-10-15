using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>没有卡牌选择 / 覆层界面 / 选目标在进行中。</summary>
internal sealed class NoSelectionGate : IRewindGateCheck
{
    public string Name => "无选择界面";

    public GateCheck Check()
    {
        if (RewindUiProbe.IsInCardSelection)
        {
            return new GateCheck(Name, false, "card selection is open");
        }

        if (RewindUiProbe.IsTargeting)
        {
            return new GateCheck(Name, false, "targeting in progress");
        }

        var overlays = RewindUiProbe.OverlayScreenCount;
        return overlays > 0
            ? new GateCheck(Name, false, $"{overlays} overlay screen(s) open")
            : new GateCheck(Name, true, string.Empty);
    }
}
