using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding.Gates;

/// <summary>只允许单人：多人下单端改状态会让校验和分化并终止 run。</summary>
internal sealed class SingleplayerGate : IRewindGateCheck
{
    public string Name => "单人";

    public GateCheck Check() => CombatAccess.IsSingleplayer
        ? new GateCheck(Name, true, string.Empty)
        : new GateCheck(Name, false, "multiplayer run");
}
