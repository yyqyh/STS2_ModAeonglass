using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>严格模式下：有异步卡牌 VFX 在飞时拒绝回档（它们结束时会继续改卡节点）。</summary>
internal sealed class NoVfxInFlightGate(Func<bool> strictMode) : IRewindGateCheck
{
    public string Name => "无卡牌特效";

    public GateCheck Check()
    {
        if (!strictMode())
        {
            return new GateCheck(Name, true, string.Empty);
        }

        return VfxBarrier.IsBusy
            ? new GateCheck(Name, false, $"card vfx in flight: {VfxBarrier.Describe()}")
            : new GateCheck(Name, true, string.Empty);
    }
}
