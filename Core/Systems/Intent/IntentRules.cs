using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图/凋萎的开关读取单点（把 Lab C 的 <c>LabScope</c> 换成角色设置；改设置立刻生效）。
/// </summary>
/// <remarks>
/// 只有"注册类"的开关（总开关 / C11 接管）需要重启；其余全是每次现读。
/// </remarks>
internal static class IntentRules
{
    /// <summary>意图 总开关。</summary>
    public static bool MasterEnabled => AeonglassSettingStore.Current.IntentEnabled;

    /// <summary>C11：是否接管原生 <c>RollMove</c>（定稿 on）。</summary>
    public static bool ManagedRollMove => AeonglassSettingStore.Current.IntentManagedRollMove;

    /// <summary>虚无被抽到时的口径。</summary>
    public static VoidDrawMode VoidMode => AeonglassSettingStore.Current.IntentVoidMode?.Trim().ToLowerInvariant() switch
    {
        "recycle" => VoidDrawMode.RecycleEachDraw,
        "nopenalty" => VoidDrawMode.NoPenaltyRecycle,
        _ => VoidDrawMode.RemoveAfterFirstDraw,
    };

    /// <summary>回合结束未打出意图卡的去向。</summary>
    public static HandEndMode HandEnd => AeonglassSettingStore.Current.IntentHandEndOfTurn?.Trim().ToLowerInvariant() switch
    {
        "deckbottom" => HandEndMode.ToDeckBottom,
        "deckshuffle" => HandEndMode.ToDeckShuffle,
        _ => HandEndMode.ToDiscard,
    };

    /// <summary>虚无惩戒：扣血点数（0 = 不扣）。</summary>
    public static int VoidPenaltyDamage =>
        Math.Clamp(AeonglassSettingStore.Current.WitherVoidDamage, 0, 99);

    /// <summary>虚无惩戒：是否额外吃掉一张意图。</summary>
    public static bool VoidEatsIntent => AeonglassSettingStore.Current.WitherEatsIntent;

    /// <summary>一次"塞虚无"塞几张。</summary>
    public static int InjectPerUse => Math.Clamp(AeonglassSettingStore.Current.WitherInjectPerUse, 1, 9);

    /// <summary>把 DEAD / ENRAGED / STUN 之类特殊状态排除出牌堆（固定开：它们不该被当普通意图打出）。</summary>
    public static bool ExcludeSpecialStates => true;

    /// <summary>条件不满足时是否执行"强制变体"（固定开）。</summary>
    // 「强制变体」已按上级 2026-10-12 决策删除（新判定优先级 ① 就是它的正式形态）。这里不再有对应开关。

    /// <summary>一行摘要（日志 / 面板用）。</summary>
    public static string Describe() =>
        $"managed={ManagedRollMove} void={VoidMode} handEnd={HandEnd} "
        + $"penalty={VoidPenaltyDamage}/eat={VoidEatsIntent} limit=不限";
}
