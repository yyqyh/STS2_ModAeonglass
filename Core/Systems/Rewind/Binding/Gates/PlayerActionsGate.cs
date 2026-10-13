using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding.Gates;

/// <summary>玩家没被禁用操作，也不在结束回合流程里。</summary>
internal sealed class PlayerActionsGate : IRewindGateCheck
{
    public string Name => "可行动";

    public GateCheck Check()
    {
        var manager = CombatManager.Instance;
        if (manager.PlayerActionsDisabled)
        {
            return new GateCheck(Name, false, "player actions disabled");
        }

        if (manager.EndingPlayerTurnPhaseOne || manager.EndingPlayerTurnPhaseTwo)
        {
            return new GateCheck(Name, false, "ending player turn");
        }

        return new GateCheck(Name, true, string.Empty);
    }
}
