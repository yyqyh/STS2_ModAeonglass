using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>玩家没被禁用操作，也不在结束回合流程里。</summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> 的一条判定。
/// ② 挂点与时机：回档前的闸门链。
/// ③ 依赖：<c>CombatManager</c> 的禁用位与两段结束回合相位。
/// ④ ⚠️ 结束回合流程中回档会把两边状态掰到不一致 ⇒ 拒绝。
/// </remarks>
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
