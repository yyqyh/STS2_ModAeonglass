using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>逻辑侧没有挂起工作：队列 / 执行器 / 复归表 / hook 动作 / 效果深度 / 待收选择。</summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> 的一条判定。
/// ② 挂点与时机：回档前的闸门链。
/// ③ 依赖：<c>CombatAccess.IsRuntimeSettled</c>。
/// ④ ⚠️ 有挂起工作时回档，那些工作会在恢复之后继续跑并改到刚恢复的状态。
/// </remarks>
internal sealed class RuntimeSettledGate : IRewindGateCheck
{
    public string Name => "无挂起工作";

    public GateCheck Check() =>
        CombatAccess.IsRuntimeSettled(out var reason)
            ? new GateCheck(Name, true, string.Empty)
            : new GateCheck(Name, false, reason);
}
