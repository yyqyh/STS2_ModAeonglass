using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding.Gates;

/// <summary>玩家侧，且所有玩家都处在可出牌的 Play 相位。</summary>
internal sealed class PlayPhaseGate : IRewindGateCheck
{
    public string Name => "Play 相位";

    public GateCheck Check()
    {
        var state = CombatAccess.State;
        if (state is null)
        {
            return new GateCheck(Name, false, "no combat state");
        }

        if (state.CurrentSide != CombatSide.Player)
        {
            return new GateCheck(Name, false, "not player side");
        }

        var notReady = state.Players.FirstOrDefault(
            p => p.PlayerCombatState is null || p.PlayerCombatState.Phase != PlayerTurnPhase.Play);
        return notReady is null
            ? new GateCheck(Name, true, string.Empty)
            : new GateCheck(Name, false, $"phase is {notReady.PlayerCombatState?.Phase}");
    }
}
