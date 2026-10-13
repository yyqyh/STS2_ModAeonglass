using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 二级资源（如永世沙漏的沙砾 / 空隙）的快照桥。
/// 本实验 mod 目前没有真实资源，先只留接口；接入时用 RitsuLib
/// <c>SecondaryResourcePersistence.CreateSnapshot(combatState, includeCombatScoped: true)</c> / <c>RestoreSnapshot</c> 实现。
/// </summary>
internal interface ISecondaryResourceBridge
{
    /// <summary>是否已接上真实实现。false 时快照/恢复都跳过。</summary>
    bool Available { get; }

    IReadOnlyDictionary<string, int> Capture(Player player);

    void Restore(Player player, IReadOnlyDictionary<string, int> snapshot);
}

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
