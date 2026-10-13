using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// M6「每场战斗主动切换次数」的闸门（测试期默认上限 0 = 不限）。
/// </summary>
/// <remarks>
/// 判定方式与 M1 倒置 / M2 激活 / M4 回溯一致：记"这条记录属于哪一场战斗"，
/// <b>战斗一换就自动清零</b>，不需要额外的战斗开始钩子。这里用 <c>CombatState</c>
/// <b>实例引用</b>判定（不用回合号 —— M4 会把回合号往回拨）。
/// </remarks>
internal static class DeckCombatLimit
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
