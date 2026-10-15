using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 双卡组的<b>切换额度闸</b>（每场战斗主动切换次数）。
/// </summary>
/// <remarks>
/// ① 职责：查询/记一笔。
/// ② 调用时机：切副前与战斗开始重置。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class DeckSwitchLimit
{
    private static readonly Dictionary<Player, (CombatState? Combat, int Count)> Usage = [];

    /// <summary>本场战斗已经主动切换了几次。</summary>
    public static int UsedCountThisCombat(Player player) =>
        AeonglassSettingStore.DebugUnlimited
            ? 0
            : Usage.TryGetValue(player, out var usage) && ReferenceEquals(usage.Combat, CurrentCombat)
            ? usage.Count
            : 0;

    /// <summary>记一次主动切换。</summary>
    public static void Mark(Player player) =>
        Usage[player] = (CurrentCombat, UsedCountThisCombat(player) + 1);

    /// <summary>清掉本场记录（调试 / 重置用）。</summary>
    public static void Reset(Player player) => Usage.Remove(player);

    private static CombatState? CurrentCombat =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState();
}
