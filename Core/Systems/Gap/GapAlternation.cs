using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>间隙的<b>「必须交替」闸</b>：坍缩之后必须先回归，反之亦然（同一种动作不能连着做两次）。</summary>
/// <remarks>
/// ① 职责：<c>TryConsume</c> 判定本次动作是否合法并记账（+ <c>GapAction</c> 枚举）。
/// ② 挂点与时机：坍缩 / 换回动作开始之前。
/// ③ 依赖：<c>CombatState</c> + 回合号（战斗一换自动清零，不需要额外的战斗钩子）。
/// ④ ⚠️ 2026-10-14 用户口径：<b>取消「每回合 1 次激活」上限</b> ⇒ 速率只由本闸与代价（每次 2 沙砾，<c>GapStore.ActionCost</c>）来限，
/// 原来的每回合计数与设置项 <c>GapActivationsPerTurn</c> 已一并删除。文件名保留历史名（它现在是时空裂隙唯一的速率闸，改名收益不值）。
/// </remarks>
internal static class GapAlternation
{
    /// <summary>间隙的两种动作（用于「必须交替」判定）。</summary>
    public enum GapAction
    {
        /// <summary>坍缩：手牌 → 间隙。</summary>
        Collapse,

        /// <summary>回归：间隙 → 手牌。</summary>
        Recall,
    }

    /// <summary>每个玩家「这一场战斗上一次做的是哪种动作」。</summary>
    private static readonly Dictionary<Player, (CombatState? Combat, GapAction? Last)> Usage = [];

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

    /// <summary>记一次激活（只记动作类型，供交替判定）。</summary>
    public static void Mark(Player player, GapAction action) =>
        Usage[player] = (CurrentCombat, action);

    /// <summary>清掉记录（调试 / 重置用）。</summary>
    public static void Reset(Player player) => Usage.Remove(player);

    private static CombatState? CurrentCombat => CombatManager.Instance.DebugOnlyGetState();
}
