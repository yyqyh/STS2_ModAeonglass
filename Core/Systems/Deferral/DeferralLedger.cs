using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Aeonglass.Core.Systems.Deferral;

/// <summary>
/// <b>延宕</b>（规则层 §2.8.4）：把"本回合你受到的伤害"寄到 X 回合后<b>一次性</b>结算。
/// </summary>
/// <remarks>
/// <para>
/// <b>语义（用户 2026-10-13 定稿）</b>：
/// ① 延宕 X = 本回合你受到的伤害在 X 回合后结算（<b>不是分摊</b>）；
/// ② 它是延后不是免伤 —— 但<b>在它落地前结束战斗就不用还</b>（规则 3b：战斗一换，欠账作废；这条是故意的强度来源）；
/// ③ 结算形式是<b>普通伤害</b>（可被格挡）⇒ 到期那笔能用当时的格挡吃下来（所以结算点放在玩家回合结束）；
/// ④ 不可叠加：同一回合的多次延宕只算一笔；已被延宕的伤害不会被再次延宕（<c>_settling</c> 闸）。
/// </para>
/// <para>
/// <b>窗口怎么算</b>：打出的"那一回合"= 本体的 <c>TurnNumber</c>；它在<b>切到玩家回合</b>时自增，
/// 所以"窗口"天然覆盖"本回合剩余 + 紧随其后的敌人回合"（敌人那几刀正是要延宕的对象）。
/// 到期 = <c>窗口回合 + X</c>，在玩家回合结束时兑付。
/// </para>
/// <para>
/// <b>不订阅战斗结束事件</b>：每条欠账都记着它属于哪一场战斗（<c>CombatState</c> 实例引用），
/// 战斗一换就永远不会被兑付 —— 与 M1 / M2 / M4 的"记引用、环境一换自动失效"同口径。
/// </para>
/// </remarks>
internal static class DeferralLedger
{
    /// <summary>一笔待结算的欠账。</summary>
    internal sealed record Debt(CombatState Combat, int DueTurn, decimal Amount, string Source);

    /// <summary>每个玩家当前的"延宕窗口"。</summary>
    private static readonly Dictionary<Player, (CombatState Combat, int ArmedTurn, int DueTurn, int Turns)> Arms = [];

    private static readonly Dictionary<Player, List<Debt>> Debts = [];

    /// <summary>正在兑付自己发出的欠账 ⇒ 这笔不能再被延宕（规则 5b）。</summary>
    private static bool _settling;

    /// <summary>本回合的延宕窗口是否打开（只有窗口内、且不在兑付中的伤害才被延宕）。</summary>
    public static bool IsIntercepting(Player player) =>
        !_settling
        && Combat is { } combat
        && Arms.TryGetValue(player, out var arm)
        && ReferenceEquals(arm.Combat, combat)
        && arm.ArmedTurn == TurnOf(player);

    /// <summary>本回合是否已经开过窗口（"不可叠加"：同回合第二次延宕不再开新窗口）。</summary>
    public static bool IsArmedThisTurn(Player player) => IsIntercepting(player);

    /// <summary>开一个延宕窗口：从这一刻起，到本回合（含随后的敌人回合）结束为止，受到的伤害都寄到 <paramref name="turns" /> 回合后。</summary>
    public static void Arm(Player player, int turns)
    {
        if (turns <= 0 || Combat is not { } combat)
        {
            return;
        }

        var turn = TurnOf(player);
        Arms[player] = (combat, turn, turn + turns, turns);
        Const.Logger.Info(
            $"[{Const.ModId}] 延宕：窗口开启（第 {turn} 回合，{turns} 回合后 = 第 {turn + turns} 回合结算）");
    }

    /// <summary>把一笔"本该掉的血"记成欠账。</summary>
    public static void Record(Player player, decimal amount, string source)
    {
        if (amount <= 0m || Combat is not { } combat || !Arms.TryGetValue(player, out var arm))
        {
            return;
        }

        if (!Debts.TryGetValue(player, out var debts))
        {
            debts = [];
            Debts[player] = debts;
        }

        debts.Add(new Debt(combat, arm.DueTurn, amount, source));
        Const.Logger.Info(
            $"[{Const.ModId}] 延宕：{amount} 点伤害记入欠账（{source}）⇒ 第 {arm.DueTurn} 回合结算"
            + $"（累计 {PendingOf(player)}）");
    }

    /// <summary>该玩家还没结清的总欠账（本场战斗内）。</summary>
    public static decimal PendingOf(Player player)
    {
        if (!Debts.TryGetValue(player, out var debts))
        {
            return 0m;
        }

        if (Combat is { } combat)
        {
            debts.RemoveAll(debt => !ReferenceEquals(debt.Combat, combat));
        }

        return debts.Sum(debt => debt.Amount);
    }

    /// <summary>还有没结清的欠账吗（决定"延宕"能力要不要继续留在场上）。</summary>
    public static bool HasOpenDebt(Player player) => PendingOf(player) > 0m;

    /// <summary>
    /// 玩家回合结束：兑付<b>到期</b>的欠账（一次性、走普通伤害 ⇒ 可被格挡）。
    /// </summary>
    /// <returns>这一下结算了多少（0 = 没有到期的）。</returns>
    public static async Task<decimal> PayDueAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (Combat is not { } combat || !Debts.TryGetValue(player, out var debts) || debts.Count == 0)
        {
            return 0m;
        }

        // 战斗换了 ⇒ 旧账作废（规则 3b：不结算）。
        debts.RemoveAll(debt => !ReferenceEquals(debt.Combat, combat));

        var turn = TurnOf(player);
        var due = debts.Where(debt => debt.DueTurn <= turn).ToList();
        if (due.Count == 0)
        {
            return 0m;
        }

        var total = due.Sum(debt => debt.Amount);
        var sources = string.Join(" + ", due.Select(debt => debt.Source));
        foreach (var debt in due)
        {
            debts.Remove(debt);
        }

        Const.Logger.Info($"[{Const.ModId}] 延宕：到期结算 {total} 点（{sources}）⇒ 走普通伤害，可被格挡");

        _settling = true;
        try
        {
            await CreatureCmd.Damage(choiceContext, player.Creature, total, ValueProp.Move, null, null);
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] 延宕：结算失败（{ex.GetType().Name}: {ex.Message}）");
        }
        finally
        {
            _settling = false;
        }

        return total;
    }

    /// <summary>一行状态（调试面板 / 日志）。</summary>
    public static string Describe(Player player)
    {
        var armed = Arms.TryGetValue(player, out var arm) && ReferenceEquals(arm.Combat, Combat)
            ? $"窗口开在第 {arm.ArmedTurn} 回合（{arm.Turns} 回合后 → 第 {arm.DueTurn} 回合）"
            : "窗口关闭";
        return $"延宕：{armed}｜未结清 {PendingOf(player)}";
    }

    // ============================ 快照（M4 回溯：延宕是玩家侧状态，规则 6 要求一并回退）============================

    /// <summary>拍快照：每个玩家的（欠账、窗口）。</summary>
    public static Dictionary<ulong, (List<(int DueTurn, decimal Amount, string Source)> Debts, (int ArmedTurn, int DueTurn, int Turns)? Arm)> Snapshot()
    {
        var result = new Dictionary<ulong, (List<(int, decimal, string)>, (int, int, int)?)>();
        foreach (var player in Players())
        {
            var debts = Debts.TryGetValue(player, out var list)
                ? list.Where(debt => ReferenceEquals(debt.Combat, Combat))
                    .Select(debt => (debt.DueTurn, debt.Amount, debt.Source))
                    .ToList()
                : [];

            (int, int, int)? arm = Arms.TryGetValue(player, out var a) && ReferenceEquals(a.Combat, Combat)
                ? (a.ArmedTurn, a.DueTurn, a.Turns)
                : null;

            result[player.NetId] = (debts, arm);
        }

        return result;
    }

    /// <summary>回档：把欠账与窗口写回去（战斗引用用"当前这一场"重建）。</summary>
    public static void Restore(
        Dictionary<ulong, (List<(int DueTurn, decimal Amount, string Source)> Debts, (int ArmedTurn, int DueTurn, int Turns)? Arm)> snapshot)
    {
        if (Combat is not { } combat)
        {
            return;
        }

        foreach (var player in Players())
        {
            if (!snapshot.TryGetValue(player.NetId, out var entry))
            {
                Debts.Remove(player);
                Arms.Remove(player);
                continue;
            }

            Debts[player] = entry.Debts
                .Select(d => new Debt(combat, d.DueTurn, d.Amount, d.Source))
                .ToList();

            if (entry.Arm is { } arm)
            {
                Arms[player] = (combat, arm.ArmedTurn, arm.DueTurn, arm.Turns);
            }
            else
            {
                Arms.Remove(player);
            }
        }
    }

    /// <summary>清空（换 run / 调试）。</summary>
    public static void Clear()
    {
        Arms.Clear();
        Debts.Clear();
        _settling = false;
    }

    private static CombatState? Combat => CombatManager.Instance.DebugOnlyGetState();

    private static IReadOnlyList<Player> Players() => Combat?.Players ?? [];

    private static int TurnOf(Player player) => player.PlayerCombatState?.TurnNumber ?? -1;
}
