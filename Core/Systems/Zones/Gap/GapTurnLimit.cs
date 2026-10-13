using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Zones;

/// <summary>
/// M2 的两道速率闸：**每回合 1 次激活** + **坍缩 / 回归必须交替**。
/// </summary>
/// <remarks>
/// <para>
/// 2026-10-14 规则层定档（取代旧的"每场 N 次"）：速率闸 = **每回合 1 次激活**（坍缩与回归共用这一个额度），
/// 并且两者**必须交替**（坍缩之后必须先回归，反之亦然）。
/// </para>
/// <para>
/// <b>为什么必须有闸</b>：代价（沙砾）堵不住无限循环 —— 花沙砾会**等量生成空隙**（M1 的不变式），
/// 而倒置又能把空隙换回沙砾 ⇒ 沙砾池可循环。规则层把这两条明确为"不能放开的两道闸"。
/// </para>
/// <para>
/// 判定方式：记"(哪一场战斗, 第几回合)"，任一变化自动清零 —— 不需要额外的回合开始钩子。
/// </para>
/// </remarks>
internal static class GapTurnLimit
{
    /// <summary>M2 的两种动作（用于"必须交替"判定）。</summary>
    public enum GapAction
    {
        /// <summary>坍缩：手牌 → 间隙。</summary>
        Collapse,

        /// <summary>回归：间隙 → 手牌。</summary>
        Recall,
    }

    /// <summary>每个玩家"本回合已激活几次 + 这一帧属于哪场战斗的第几回合 + 上一次是什么动作"。</summary>
    private static readonly Dictionary<Player, (CombatState? Combat, int Turn, int Count, GapAction? Last)>
        Usage = [];

    /// <summary>本回合还能激活几次。</summary>
    public static int RemainingThisTurn(Player player, int limit)
        => STS2_Aeonglass.Core.Config.AeonglassSettingStore.DebugUnlimited
            ? int.MaxValue
            : Math.Max(0, limit - UsedThisTurn(player));

    /// <summary>本回合已经激活了几次（坍缩与回归共用这一个额度）。</summary>
    public static int UsedThisTurn(Player player)
    {
        if (STS2_Aeonglass.Core.Config.AeonglassSettingStore.DebugUnlimited)
        {
            return 0;
        }

        // 不在战斗状态里 ⇒ 没有"本回合"这回事（额度按全新算）。
        if (CurrentCombat is not { } combat)
        {
            return 0;
        }

        return Usage.TryGetValue(player, out var usage)
               && ReferenceEquals(usage.Combat, combat)
               && usage.Turn == TurnOf(player)
            ? usage.Count
            : 0;
    }

    /// <summary>上一次动作是什么（本场内；没做过返回 null）。</summary>
    public static GapAction? LastAction(Player player)
        => CurrentCombat is { } combat
           && Usage.TryGetValue(player, out var usage)
           && ReferenceEquals(usage.Combat, combat)
            ? usage.Last
            : null;

    /// <summary>能不能做 <paramref name="action" />：交替规则 —— 不能连续两次同一种。</summary>
    public static bool AllowsAlternation(Player player, GapAction action) =>
        LastAction(player) != action;

    /// <summary>记一次激活（本回合计数 +1，并记下动作类型供交替判定）。</summary>
    public static void Mark(Player player, GapAction action)
    {
        Usage[player] = (CurrentCombat, TurnOf(player), UsedThisTurn(player) + 1, action);
    }

    /// <summary>清掉记录（调试 / 重置用）。</summary>
    public static void Reset(Player player) => Usage.Remove(player);

    private static CombatState? CurrentCombat => CombatManager.Instance.DebugOnlyGetState();

    /// <summary>
    /// 这个玩家<b>当前是第几回合</b>。
    /// </summary>
    /// <remarks>
    /// 用 <c>PlayerCombatState.TurnNumber</c>（每个玩家各自一份，<b>每场战斗从 1 重新数</b>），
    /// <b>不是</b> <c>ICombatState.RoundNumber</c>（那是"轮次"，多人下所有玩家共用）。
    /// 拿不到战斗状态时返回 <c>-1</c> ⇒ 与任何已记录的回合都不等 ⇒ 视为"新回合"（额度归零）。
    /// </remarks>
    private static int TurnOf(Player player) => player.PlayerCombatState?.TurnNumber ?? -1;
}
