namespace STS2_Aeonglass.Core.Systems.EnemyTime;

/// <summary>
/// **系统层 → 卡带（表现层）的唯一接缝**：指向模式开关时通知卡带"切可点 / 切回只读"。
/// </summary>
/// <remarks>
/// 为什么要有这层：M3 的系统层（<see cref="IntentTargeting" />）不该直接引用表现层的节点类型。
/// 卡带挂载时把自己的静态方法注册进来；没注册（比如卡带没挂）时这里就是空操作，不影响机制。
/// </remarks>
internal static class MonsterIntentBeltPresent
{
    /// <summary>由卡带注册："把可点状态切成 on"。</summary>
    public static Action<bool>? TargetingChanged { get; set; }

    public static void SetTargeting(bool on)
    {
        try
        {
            TargetingChanged?.Invoke(on);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} 卡带切换可点状态失败（已忽略）：{ex.Message}");
        }
    }
}
