using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.History;

/// <summary>
/// 战斗历史<b>数据层</b>：本回合/上回合的出牌数、翻转数、回溯数等。
/// </summary>
/// <remarks>
/// ① 职责：打点与读取（年轮/刻痕/因果等卡读它）。
/// ② 调用时机：出牌、回合切换、翻转/回溯事件。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class CombatHistory
{
    private sealed class Record
    {
        public CombatState? Combat;
        public int Turn = -1;
        public int PlayedThisTurn;
        public int AttacksThisTurn;
        public int IntentManipulationsThisTurn;
        public int PlayedLastTurn;
        public int AttacksLastTurn;
        public int IntentManipulationsLastTurn;
        public int InvertsThisCombat;
        public int RewindsThisCombat;
        public int SandSpentThisCombat;
    }

    private static readonly Dictionary<Player, Record> Records = [];

    // ============================ 写入（各机制在自己的单入口打点）============================

    /// <summary>记"打出了一张牌"（由遗物的 AfterCardPlayed 调）。</summary>
    public static void RecordCardPlayed(Player player, CardModel card)
    {
        var record = Current(player);
        record.PlayedThisTurn++;
        if (card.Type == CardType.Attack)
        {
            record.AttacksThisTurn++;
        }
    }

    /// <summary>记"这个玩家的回合结束了"——把本回合读数滚成"上回合"。</summary>
    public static void EndTurn(Player player)
    {
        var record = Current(player);
        record.PlayedLastTurn = record.PlayedThisTurn;
        record.AttacksLastTurn = record.AttacksThisTurn;
        record.IntentManipulationsLastTurn = record.IntentManipulationsThisTurn;
        record.PlayedThisTurn = 0;
        record.AttacksThisTurn = 0;
        record.IntentManipulationsThisTurn = 0;
    }

    /// <summary>记一次翻转（沙砾与空隙对调）。</summary>
    public static void RecordInvert(Player player) => Current(player).InvertsThisCombat++;

    /// <summary>记一次成功的回溯。</summary>
    public static void RecordRewind(Player player) => Current(player).RewindsThisCombat++;

    /// <summary>记一笔沙砾消耗（不管什么原因花的）。</summary>
    public static void RecordSandSpent(Player player, int amount)
        => Current(player).SandSpentThisCombat += Math.Max(0, amount);

    /// <summary>记一次"操作了怪物意图"（推迟 / 提前 / 剥夺 / 塞虚无 / 改属性）。</summary>
    public static void RecordIntentManipulation(Player player) => Current(player).IntentManipulationsThisTurn++;

    // ============================ 读取 ============================

    /// <summary>上回合打出了几张牌。</summary>
    public static int CardsPlayedLastTurn(Player player) => Raw(player).PlayedLastTurn;

    /// <summary>
    /// 本回合**已经**打出了几张牌（不含正在结算的这一张 —— 记录发生在牌结算之后）。
    /// </summary>
    /// <remarks>供"若这是本回合第一张牌…"类卡判定：在卡的 OnPlay 里读，0 就说明它是第一张。</remarks>
    public static int CardsPlayedThisTurn(Player player) => Raw(player).PlayedThisTurn;

    /// <summary>上回合打出了几张<b>攻击</b>牌。</summary>
    public static int AttacksPlayedLastTurn(Player player) => Raw(player).AttacksLastTurn;

    /// <summary>本场一共翻转了几次。</summary>
    public static int InvertsThisCombat(Player player) => Raw(player).InvertsThisCombat;

    /// <summary>本场一共回溯了几次。</summary>
    public static int RewindsThisCombat(Player player) => Raw(player).RewindsThisCombat;

    /// <summary>本场一共消耗了多少沙砾。</summary>
    public static int SandSpentThisCombat(Player player) => Raw(player).SandSpentThisCombat;

    /// <summary>上回合操作过几次怪物意图（INT-6 高维观测读它）。</summary>
    public static int IntentManipulationsLastTurn(Player player) => Raw(player).IntentManipulationsLastTurn;

    /// <summary>清空（换 run / 调试）。</summary>
    public static void Clear() => Records.Clear();

    /// <summary>一行状态（面板 / 日志）。</summary>
    public static string Describe(Player player)
    {
        var record = Raw(player);
        return $"上回合出牌 {record.PlayedLastTurn}（攻击 {record.AttacksLastTurn}）"
               + $"｜本场 翻转 {record.InvertsThisCombat} / 回溯 {record.RewindsThisCombat}"
               + $" / 消耗沙砾 {record.SandSpentThisCombat}";
    }

    // ============================ 内部 ============================

    /// <summary>取"当前这场战斗"的记录；战斗换了 / 回合变了就做对应的滚动。</summary>
    private static Record Current(Player player)
    {
        var record = Raw(player);
        var combat = CurrentCombat;
        var turn = TurnOf(player);

        if (!ReferenceEquals(record.Combat, combat))
        {
            // 换了一场战斗 ⇒ 一切从头（全场统计也重来：这些卡的口径都是"本场"）。
            record.Combat = combat;
            record.Turn = turn;
            record.PlayedThisTurn = 0;
            record.AttacksThisTurn = 0;
            record.IntentManipulationsThisTurn = 0;
            record.PlayedLastTurn = 0;
            record.AttacksLastTurn = 0;
            record.IntentManipulationsLastTurn = 0;
            record.InvertsThisCombat = 0;
            record.RewindsThisCombat = 0;
            record.SandSpentThisCombat = 0;
            return record;
        }

        if (record.Turn != turn)
        {
            // 同一场战斗、进了新回合 ⇒ 旧的本回合读数成为"上回合"。
            record.PlayedLastTurn = record.PlayedThisTurn;
            record.AttacksLastTurn = record.AttacksThisTurn;
            record.IntentManipulationsLastTurn = record.IntentManipulationsThisTurn;
            record.PlayedThisTurn = 0;
            record.AttacksThisTurn = 0;
            record.IntentManipulationsThisTurn = 0;
            record.Turn = turn;
        }

        return record;
    }

    /// <summary>只读：不改动任何状态（读取方别顺手触发滚动语义）。</summary>
    private static Record Raw(Player player)
    {
        if (!Records.TryGetValue(player, out var record))
        {
            record = new Record();
            Records[player] = record;
        }

        return record;
    }

    private static CombatState? CurrentCombat => CombatManager.Instance.DebugOnlyGetState();

    private static int TurnOf(Player player) => player.PlayerCombatState?.TurnNumber ?? -1;
}
