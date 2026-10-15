using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

namespace STS2_Aeonglass.Core.Systems.Rewind;

/// <summary>回溯的"每场战斗 1 次"闸门（定稿参数）。</summary>
/// <remarks>
/// ① 职责：<c>TryConsume</c> 判定本场是否还有额度并记账。
/// ② 挂点与时机：<c>RewindFacade</c> 的闸门评估链里调（真正回档之前）。
/// ③ 依赖：<c>CombatState</c> 实例引用 + 设置项。
/// ④ ⚠️ 键必须记 <c>CombatState</c> <b>实例引用</b>而不是回合号：回溯会把回合号往回拨，用回合号当键会被自己的回档重置掉（等于没闸门）；
/// 战斗一换就自动允许，不需要额外的战斗开始钩子（判定方式与倒置 / 坍缩的闸门一致）。
/// </remarks>
internal static class RewindCombatLimit
{
    private static readonly Dictionary<Player, (CombatState Combat, int Count)> UsedIn = [];

    /// <summary>本场额外允许多回溯几次（RW-4 双线给的额度）。</summary>
    private static readonly Dictionary<Player, (CombatState Combat, int Extra)> ExtraIn = [];

    /// <summary>本场战斗是否已经用掉那一次。</summary>
    public static bool Used(Player player) =>
        !AeonglassSettingStore.DebugUnlimited
        && CombatAccess.State is { } state
        && UsedIn.TryGetValue(player, out var used)
        && ReferenceEquals(used.Combat, state)
        && used.Count > Extra(player);

    /// <summary>本场额外额度（RW-4 双线加；战斗一换自动归零）。</summary>
    public static int Extra(Player player) =>
        CombatAccess.State is { } state
        && ExtraIn.TryGetValue(player, out var extra)
        && ReferenceEquals(extra.Combat, state)
            ? extra.Extra
            : 0;

    /// <summary>加一份本场额度。</summary>
    public static void GrantExtra(Player player, int count)
    {
        if (CombatAccess.State is not { } state || count <= 0)
        {
            return;
        }

        ExtraIn[player] = (state, Extra(player) + count);
        Const.Logger.Info($"[{Const.ModId}] [回溯] 本场额外回溯额度 +{count}（现共 {Extra(player)} 次）");
    }

    /// <summary>占掉本场的那一次（先占位，避免连点重复触发）。</summary>
    public static void Mark(Player player)
    {
        if (CombatAccess.State is { } state)
        {
            var count = UsedIn.TryGetValue(player, out var used) && ReferenceEquals(used.Combat, state)
                ? used.Count + 1
                : 1;
            UsedIn[player] = (state, count);
        }
    }

    /// <summary>退回占位（回档没成立时用，例如没有上一回合快照）。</summary>
    public static void Unmark(Player player)
    {
        if (CombatAccess.State is { } state
            && UsedIn.TryGetValue(player, out var used)
            && ReferenceEquals(used.Combat, state)
            && used.Count > 1)
        {
            UsedIn[player] = (state, used.Count - 1);
            return;
        }

        UsedIn.Remove(player);
    }
}
