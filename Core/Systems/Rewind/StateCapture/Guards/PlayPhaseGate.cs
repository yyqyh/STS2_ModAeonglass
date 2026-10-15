using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>玩家侧，且所有玩家都处在可出牌的 Play 相位。</summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> 的一条判定。
/// ② 挂点与时机：回档前的闸门链。
/// ③ 依赖：<c>CombatAccess.State</c>（当前阵营 + 各玩家相位）。
/// ④ ⚠️ 要求<b>所有</b>玩家都在 Play 相位：联机时另一头在别的相位就不能回档。
/// </remarks>
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
