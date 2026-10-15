using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>严格模式下：有异步卡牌 VFX 在飞时拒绝回档（它们结束时会继续改卡节点）。</summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> 的一条判定。
/// ② 挂点与时机：回档前的闸门链。
/// ③ 依赖：构造时注入的 <c>strictMode</c> 判定 + <c>VfxBarrier</c>。
/// ④ ⚠️ 非严格模式直接放行；在飞的 VFX 结束时还会继续改卡节点 ⇒ 严格模式下必须等它落地。
/// </remarks>
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
