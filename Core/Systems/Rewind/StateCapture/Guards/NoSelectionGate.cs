using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>没有卡牌选择 / 覆层界面 / 选目标在进行中。</summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> 的一条判定。
/// ② 挂点与时机：回档前的闸门链。
/// ③ 依赖：<c>RewindUiProbe</c>（选择 / 选目标 / 覆层三个探测位）。
/// ④ ⚠️ 界面开着时回档，那些界面会指向已经失效的卡 ⇒ 一律拒绝。
/// </remarks>
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
