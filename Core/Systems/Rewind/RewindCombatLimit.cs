using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Rewind.Binding;

namespace STS2_Aeonglass.Core.Systems.Rewind;

/// <summary>
/// M4 的"每场战斗 1 次"闸门（定稿参数）。
/// </summary>
/// <remarks>
/// <para>
/// 判定方式和 M1 倒置 / M2 激活一样：记"这条记录属于哪一场战斗"，**战斗一换就自动允许** ——
/// 不需要额外的战斗开始钩子来重置。
/// </para>
/// <para>
/// 这里记的是 <c>CombatState</c> <b>实例引用</b>（不是回合号）：回溯会把回合号往回拨，
/// 用回合号当键会被自己的回档重置掉，等于没有闸门。
/// </para>
/// </remarks>
internal static class RewindCombatLimit
{
    private static readonly Dictionary<Player, CombatState> UsedIn = [];

    /// <summary>本场战斗是否已经用掉那一次。</summary>
    public static bool Used(Player player) =>
        !AeonglassSettingStore.DebugUnlimited
        && UsedIn.TryGetValue(player, out var combat)
        && ReferenceEquals(combat, CombatAccess.State);

    /// <summary>占掉本场的那一次（先占位，避免连点重复触发）。</summary>
    public static void Mark(Player player)
    {
        if (CombatAccess.State is { } state)
        {
            UsedIn[player] = state;
        }
    }

    /// <summary>退回占位（回档没成立时用，例如没有上一回合快照）。</summary>
    public static void Unmark(Player player) => UsedIn.Remove(player);
}
