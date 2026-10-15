using MegaCrit.Sts2.Core.Entities.Players;


namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>占位实现：永远"未接入"，调用方据此跳过并写日志。</summary>
internal sealed class NullSecondaryResourceBridge : ISecondaryResourceBridge
{
    public static NullSecondaryResourceBridge Instance { get; } = new();

    public bool Available => false;

    public IReadOnlyDictionary<string, int> Capture(Player player) =>
        new Dictionary<string, int>();

    public void Restore(Player player, IReadOnlyDictionary<string, int> snapshot)
    {
    }
}
