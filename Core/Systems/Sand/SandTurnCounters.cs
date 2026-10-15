using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Sand;

/// <summary>时之沙的<b>回合内计数</b>：本回合翻转了几次沙漏（给"翻转联动"类卡读）。</summary>
/// <remarks>
/// <para>
/// 记账方式与 <c>GapAlternation</c> 一致：记"这条记录属于哪场战斗的第几回合"，任一变化自动归零
/// —— 不需要额外的回合开始钩子。
/// </para>
/// <para>
/// ⚠️ 这与"时之沙的每回合次数上限"是两回事：上限早已取消（Step 1），这里只是**纯计数**，不拦任何操作。
/// </para>
/// </remarks>
internal static class SandTurnCounters
{
    private static readonly Dictionary<Player, (CombatState? Combat, int Turn, int Inverts, int Spent, int SpentPrev, bool Changed)>
        Usage = [];

    /// <summary>本回合翻转了几次。</summary>
    public static int InvertsThisTurn(Player player)
        => Usage.TryGetValue(player, out var usage)
           && ReferenceEquals(usage.Combat, CurrentCombat)
           && usage.Turn == TurnOf(player)
            ? usage.Inverts
            : 0;

    /// <summary>本回合一共消耗了多少沙砾（"消耗过沙砾"类条件的判据）。</summary>
    public static int SandsSpentThisTurn(Player player)
        => Usage.TryGetValue(player, out var usage)
           && ReferenceEquals(usage.Combat, CurrentCombat)
           && usage.Turn == TurnOf(player)
            ? usage.Spent
            : 0;

    /// <summary>本回合有没有消耗过沙砾。</summary>
    public static bool SpentSandThisTurn(Player player) => SandsSpentThisTurn(player) > 0;

    /// <summary>
    /// <b>本回合沙砾值变动过</b>（翻转 / 消耗 / 获得<b>都算</b>）—— "沙砾变换过"类条件的唯一读口。
    /// </summary>
    /// <remarks>
    /// 与"翻转过 / 消耗过"分开记：那两条是特化条件（卡表里点名要求），这条是 2026-10-15 用户口径下的宽条件
    /// （例：INV-1 回摆）。记账统一由 <c>AfterSecondaryResourceChanged</c> 这条资源变动钩子灌入。
    /// </remarks>
    public static bool SandChangedThisTurn(Player player)
        => Usage.TryGetValue(player, out var usage)
           && ReferenceEquals(usage.Combat, CurrentCombat)
           && usage.Turn == TurnOf(player)
           && usage.Changed;

    /// <summary>**上回合**消耗了多少沙砾（"上回合消耗过"类条件的判据）。</summary>
    public static int SandsSpentLastTurn(Player player)
        => Usage.TryGetValue(player, out var usage)
           && ReferenceEquals(usage.Combat, CurrentCombat)
           && usage.Turn == TurnOf(player)
            ? usage.SpentPrev
            : 0;

    /// <summary>记一次翻转（由 <see cref="SandInvertCmd.TryInvert" /> 成功时调用）。</summary>
    public static void MarkInvert(Player player)
        => Usage[player] = Carry(player, InvertsThisTurn(player) + 1, SandsSpentThisTurn(player), changed: true);

    /// <summary>记一笔沙砾消耗（由"支付沙砾"的订阅入口调用）。</summary>
    public static void MarkSandSpent(Player player, int amount)
        => Usage[player] = Carry(
            player,
            InvertsThisTurn(player),
            SandsSpentThisTurn(player) + Math.Max(0, amount),
            changed: true);

    /// <summary>记一笔"沙砾值变过"（获得 / 其它来源；翻转与消耗已在各自入口里带上这个标记）。</summary>
    public static void MarkSandChanged(Player player)
        => Usage[player] = Carry(player, InvertsThisTurn(player), SandsSpentThisTurn(player), changed: true);

    /// <summary>
    /// 写入本回合读数，并**把上一回合的读数保留成 SpentPrev**。
    /// </summary>
    /// <remarks>跨回合的第一笔写入就把"旧的 Spent"挪进 SpentPrev；同一回合内的后续写入不动 SpentPrev。</remarks>
    private static (CombatState? Combat, int Turn, int Inverts, int Spent, int SpentPrev, bool Changed) Carry(
        Player player,
        int inverts,
        int spent,
        bool changed)
    {
        var combat = CurrentCombat;
        var turn = TurnOf(player);
        var previousSpent = 0;
        var changedAlready = false;

        if (Usage.TryGetValue(player, out var old)
            && ReferenceEquals(old.Combat, combat)
            && old.Turn == turn)
        {
            previousSpent = old.SpentPrev;
            changedAlready = old.Changed;
        }
        else if (Usage.TryGetValue(player, out var stale) && ReferenceEquals(stale.Combat, combat))
        {
            // 同一场战斗、但回合号变了 ⇒ 旧回合的消耗额成为"上回合"。
            previousSpent = stale.Spent;
        }

        return (combat, turn, inverts, spent, previousSpent, changed || changedAlready);
    }

    /// <summary>清掉记录（调试用）。</summary>
    public static void Reset(Player player) => Usage.Remove(player);

    private static CombatState? CurrentCombat => CombatManager.Instance.DebugOnlyGetState();

    /// <summary>当前回合号（每人一份、每场从 1 起数；拿不到战斗状态时返回 -1 ⇒ 视为新回合）。</summary>
    private static int TurnOf(Player player) => player.PlayerCombatState?.TurnNumber ?? -1;
}
