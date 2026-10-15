using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>必须在战斗中，且战斗状态可读。</summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> 的一条判定。
/// ② 挂点与时机：回档前的闸门链（<c>RewindFacade.EvaluateGates</c>）逐个跑。
/// ③ 依赖：<c>CombatManager.IsInProgress</c>、<c>CombatAccess.State</c>。
/// ④ ⚠️ 战斗状态读不到时按"不通过"处理（宁可拒绝回档）。
/// </remarks>
internal sealed class InCombatGate : IRewindGateCheck
{
    public string Name => "战斗中";

    public GateCheck Check()
    {
        if (!CombatManager.Instance.IsInProgress)
        {
            return new GateCheck(Name, false, "combat is not in progress");
        }

        return CombatAccess.State is null
            ? new GateCheck(Name, false, "combat state unavailable")
            : new GateCheck(Name, true, string.Empty);
    }
}
