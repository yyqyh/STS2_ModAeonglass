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

    /// <summary>
    /// 一次"获得延宕"＝<b>一个独立实例</b>（规则 §2.8.4 第 5c 条，2026-10-15 用户确认）：
    /// 各带自己的到期回合；同一个实例窗口内记到的伤害汇成它自己的待结算量。
    /// </summary>
    internal sealed record ArmInstance(CombatState Combat, int ArmedTurn, int DueTurn, int Turns);

    /// <summary>每个玩家的延宕<b>实例列表</b>（每次获得都追加一条，而不是覆盖一个全局变量）。</summary>
    private static readonly Dictionary<Player, List<ArmInstance>> Arms = [];

    private static readonly Dictionary<Player, List<Debt>> Debts = [];

    /// <summary>正在兑付自己发出的欠账 ⇒ 这笔不能再被延宕（规则 5b）。</summary>
    private static bool _settling;

    /// <summary>本回合的延宕窗口是否打开（只有窗口内、且不在兑付中的伤害才被延宕）。</summary>
    public static bool IsIntercepting(Player player) => !_settling && OpenArmOf(player) is not null;

    /// <summary>本回合还有开着的窗口吗（寿命判定用；"不可叠加"指的是同一笔伤害不会被二次延宕）。</summary>
    public static bool IsArmedThisTurn(Player player) => OpenArmOf(player) is not null;

    /// <summary>还有没结算完的实例吗（延宕能力是否该继续留在场上）。</summary>
    public static bool HasUnsettledInstance(Player player)
    {
        if (Combat is not { } combat || !Arms.TryGetValue(player, out var arms))
        {
            return false;
        }

        var turn = TurnOf(player);
        arms.RemoveAll(arm => !ReferenceEquals(arm.Combat, combat) || arm.DueTurn < turn);
        return arms.Any(arm => arm.DueTurn > turn);
    }

    /// <summary>
    /// <b>新增一个延宕实例</b>：从这一刻起，到本回合（含随后的敌人回合）结束为止，受到的伤害寄到
    /// <paramref name="turns" /> 回合后。多次获得 ＝ 多条实例，各自独立结算（规则 5c）。
    /// </summary>
    public static void Arm(Player player, int turns)
    {
        if (turns <= 0 || Combat is not { } combat)
        {
            return;
        }

        if (!Arms.TryGetValue(player, out var arms))
        {
            arms = [];
            Arms[player] = arms;
        }

        arms.RemoveAll(arm => !ReferenceEquals(arm.Combat, combat));

        var turn = TurnOf(player);
        arms.Add(new ArmInstance(combat, turn, turn + turns, turns));
        Const.Logger.Info(
            $"[{Const.ModId}] 延宕：新增实例 #{arms.Count}（第 {turn} 回合获得，"
            + $"{turns} 回合后 = 第 {turn + turns} 回合结算）");
    }

    /// <summary>把一笔"本该掉的血"记成欠账。</summary>
    public static void Record(Player player, decimal amount, string source)
    {
        if (amount <= 0m || Combat is not { } combat || OpenArmOf(player) is not { } arm)
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

    /// <summary>本回合最新打开的那个实例（同一笔伤害只记进它，不会被二次延宕）。</summary>
    private static ArmInstance? OpenArmOf(Player player)
        => Combat is { } combat && Arms.TryGetValue(player, out var arms)
            ? arms.LastOrDefault(arm => ReferenceEquals(arm.Combat, combat) && arm.ArmedTurn == TurnOf(player))
            : null;

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
        var live = Arms.TryGetValue(player, out var arms) && Combat is { } combat
            ? arms.Where(arm => ReferenceEquals(arm.Combat, combat)).ToList()
            : [];

        var instances = live.Count == 0
            ? "无实例"
            : string.Join(
                " + ",
                live.Select(arm => $"#{arm.ArmedTurn}→第 {arm.DueTurn} 回合"));

        return $"延宕：{instances}｜未结清 {PendingOf(player)}";
    }

    // ============================ 快照（M4 回溯：延宕是玩家侧状态，规则 6 要求一并回退）============================

    /// <summary>拍快照：每个玩家的（欠账、窗口）。</summary>
    public static Dictionary<ulong, (List<(int DueTurn, decimal Amount, string Source)> Debts, List<(int ArmedTurn, int DueTurn, int Turns)> Arms)> Snapshot()
    {
        var result = new Dictionary<ulong, (List<(int, decimal, string)>, List<(int, int, int)>)>();
        foreach (var player in Players())
        {
            var debts = Debts.TryGetValue(player, out var list)
                ? list.Where(debt => ReferenceEquals(debt.Combat, Combat))
                    .Select(debt => (debt.DueTurn, debt.Amount, debt.Source))
                    .ToList()
                : [];

            var arms = Arms.TryGetValue(player, out var list2)
                ? list2.Where(arm => ReferenceEquals(arm.Combat, Combat))
                    .Select(arm => (arm.ArmedTurn, arm.DueTurn, arm.Turns))
                    .ToList()
                : [];

            result[player.NetId] = (debts, arms);
        }

        return result;
    }

    /// <summary>回档：把欠账与窗口写回去（战斗引用用"当前这一场"重建）。</summary>
    public static void Restore(
        Dictionary<ulong, (List<(int DueTurn, decimal Amount, string Source)> Debts, List<(int ArmedTurn, int DueTurn, int Turns)> Arms)> snapshot)
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

            Arms[player] = entry.Arms
                .Select(arm => new ArmInstance(combat, arm.ArmedTurn, arm.DueTurn, arm.Turns))
                .ToList();
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
