using MegaCrit.Sts2.Core.Entities.Players;


namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>次级资源接缝的占位实现：永远"未接入"，调用方据此跳过并写日志。</summary>
/// <remarks>
/// ① 职责：<c>Available => false</c> + 空实现（单例 <c>Instance</c>）。
/// ② 挂点与时机：<c>ExtensionRegistry</c> 未装真实实现时的默认值。
/// ③ 依赖：<c>ISecondaryResourceBridge</c>。
/// ④ ⚠️ 存在意义是让"没接次级资源"这件事<b>显式可见</b>，而不是静默丢掉资源快照。
/// </remarks>
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
