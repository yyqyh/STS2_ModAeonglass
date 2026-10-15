using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>玩家尚未 ready。ready 之后结束回合信号不可撤，此时改回合号会污染日志与遥测。</summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> 的一条判定。
/// ② 挂点与时机：回档前的闸门链。
/// ③ 依赖：<c>CombatAccess.LocalPlayer</c>、<c>CombatManager.IsPlayerReadyToEndTurn</c>。
/// ④ ⚠️ 已 ready 时不能回档：结束回合信号不可撤，改回合号会污染日志与遥测。
/// </remarks>
internal sealed class NotReadyGate : IRewindGateCheck
{
    public string Name => "未 ready";

    public GateCheck Check()
    {
        var player = CombatAccess.LocalPlayer;
        if (player is null)
        {
            return new GateCheck(Name, false, "no local player");
        }

        return CombatManager.Instance.IsPlayerReadyToEndTurn(player)
            ? new GateCheck(Name, false, "player already ready to end turn")
            : new GateCheck(Name, true, string.Empty);
    }
}
