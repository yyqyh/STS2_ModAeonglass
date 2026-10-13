using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding.Gates;

/// <summary>必须在战斗中，且战斗状态可读。</summary>
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
