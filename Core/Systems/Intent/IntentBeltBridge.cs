namespace STS2_Aeonglass.Core.Systems.Intent;

using MegaCrit.Sts2.Core.Entities.Creatures;

/// <summary>
/// **系统层 → 卡带（表现层）的唯一接缝**：指向模式开关时通知卡带"切可点 / 切回只读"。
/// </summary>
/// <remarks>
/// 为什么要有这层：意图的系统层（<see cref="IntentTargeting" />）不该直接引用表现层的节点类型。
/// 卡带挂载时把自己的静态方法注册进来；没注册（比如卡带没挂）时这里就是空操作，不影响机制。
/// </remarks>
internal static class IntentBeltBridge
{
    /// <summary>由卡带注册："把可点状态切成 on"。</summary>
    public static Action<bool>? TargetingChanged { get; set; }

    /// <summary>
    /// 由卡带注册："在<b>指定那只怪</b>的卡带上打开「抽牌堆顶选牌」面板"（INT-4 拆解用）。
    /// </summary>
    /// <remarks>签名 = (怪, 候选卡, 点选回调) ⇒ 是否成功打开；卡带没挂 / 那只怪没有卡带都会返回 false。</remarks>
    public static Func<Creature, IReadOnlyList<IntentCard>, Func<IntentCard, string>, bool>? TopPickRequested
    {
        get;
        set;
    }

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

    /// <summary>打开"抽牌堆顶选牌"面板；没接缝 / 没卡带就返回 false（调用方按"没选到"处理，不动牌）。</summary>
    public static bool BeginTopPick(
        Creature creature,
        IReadOnlyList<IntentCard> cards,
        Func<IntentCard, string> onPick)
    {
        try
        {
            return TopPickRequested?.Invoke(creature, cards, onPick) ?? false;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} 打开拆解面板失败（已忽略）：{ex.Message}");
            return false;
        }
    }
}
