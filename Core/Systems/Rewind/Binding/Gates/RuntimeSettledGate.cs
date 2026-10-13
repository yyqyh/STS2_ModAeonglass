using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding.Gates;

/// <summary>逻辑侧没有挂起工作：队列 / 执行器 / 复归表 / hook 动作 / 效果深度 / 待收选择。</summary>
internal sealed class RuntimeSettledGate : IRewindGateCheck
{
    public string Name => "无挂起工作";

    public GateCheck Check() =>
        CombatAccess.IsRuntimeSettled(out var reason)
            ? new GateCheck(Name, true, string.Empty)
            : new GateCheck(Name, false, reason);
}
