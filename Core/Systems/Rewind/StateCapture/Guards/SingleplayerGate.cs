using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;

/// <summary>只允许单人：多人下单端改状态会让校验和分化并终止 run。</summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> 的一条判定。
/// ② 挂点与时机：回档前的闸门链。
/// ③ 依赖：<c>CombatAccess.IsSingleplayer</c>。
/// ④ ⚠️ 联机一律拒绝 —— 单端改状态会让两端校验和分化并终止 run。
/// </remarks>
internal sealed class SingleplayerGate : IRewindGateCheck
{
    public string Name => "单人";

    public GateCheck Check() => CombatAccess.IsSingleplayer
        ? new GateCheck(Name, true, string.Empty)
        : new GateCheck(Name, false, "multiplayer run");
}
