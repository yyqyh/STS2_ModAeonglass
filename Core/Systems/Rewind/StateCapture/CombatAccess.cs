using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 战斗态只读访问口（给接缝与探针读，不写）。
/// </summary>
/// <remarks>
/// ① 职责：取战斗 / 玩家 / 牌堆等只读视图。
/// ② 调用时机：接缝与调试面板。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class CombatAccess
{
    internal static CombatState? State => CombatManager.Instance.DebugOnlyGetState();

    internal static object? TurnState =>
        Reflect.Get<object>(CombatManager.Instance, Coordinates.CombatManager.TurnState);

    internal static Player? LocalPlayer
    {
        get
        {
            var state = State;
            return state is null ? null : LocalContext.GetMe(state);
        }
    }

    internal static bool IsSingleplayer => RunManager.Instance.IsSingleplayerOrFakeMultiplayer;

    internal static int TurnNumber(Player? player) =>
        player?.PlayerCombatState is { } combatState
        && Reflect.TryGet<int>(combatState, Coordinates.PlayerCombatState.TurnNumber, out var value)
            ? value
            : 0;

    internal static int CurrentTurnNumber() => TurnNumber(LocalPlayer);

    /// <summary>
    /// 逻辑侧"没有挂起工作"：队列空 + 执行器闲 + 复归表空 + hook 动作空 + 效果深度全 0 + 无待收选择。
    /// 这是安全改状态的硬前提（规格 §3）。
    /// </summary>
    internal static bool IsRuntimeSettled(out string reason)
    {
        if (!RunManager.Instance.ActionQueueSet.IsEmpty)
        {
            reason = "action queue is not empty";
            return false;
        }

        var executor = RunManager.Instance.ActionExecutor;
        if (executor.IsRunning || executor.CurrentlyRunningAction is not null)
        {
            reason = "action executor is busy";
            return false;
        }

        if (Reflect.Count(RunManager.Instance.ActionQueueSet, Coordinates.ActionQueueSet.WaitingForResumption) > 0)
        {
            reason = "an action is waiting for a player choice";
            return false;
        }

        var synchronizer = RunManager.Instance.ActionQueueSynchronizer;
        if (Reflect.Count(synchronizer, Coordinates.ActionQueueSynchronizer.HookActions) > 0
            || Reflect.Count(synchronizer, Coordinates.ActionQueueSynchronizer.RequestedWaitingPlayerTurn) > 0)
        {
            reason = "synchronized actions are still pending";
            return false;
        }

        if (!Reflect.AllZeroValues(CombatManager.Instance, Coordinates.CombatManager.CardOrPotionEffectDepth))
        {
            reason = "card or potion effect is still executing";
            return false;
        }

        if (Reflect.Count(RunManager.Instance.PlayerChoiceSynchronizer, Coordinates.PlayerChoiceSynchronizer.ReceivedChoices) > 0)
        {
            reason = "a player choice is pending";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>面板 / 日志用的一行摘要。</summary>
    internal static string Describe()
    {
        var state = State;
        var player = LocalPlayer;
        if (state is null || player?.PlayerCombatState is not { } combat)
        {
            return "no combat";
        }

        var powers = player.Creature.Powers.Count();
        var history = CombatManager.Instance.History.Entries.Count();
        return $"T{CurrentTurnNumber()} {DescribeVitals()} energy {combat.Energy} stars {combat.Stars} "
            + $"hand {combat.Hand.Cards.Count} draw {combat.DrawPile.Cards.Count} "
            + $"discard {combat.DiscardPile.Cards.Count} exhaust {combat.ExhaustPile.Cards.Count} "
            + $"powers {powers} hist {history}";
    }

    /// <summary>我方 power 明细（"力量=3,易伤=2"），用于验证回档是否真的写回层数。</summary>
    internal static string DescribePowers()
    {
        var player = LocalPlayer;
        if (player is null)
        {
            return "none";
        }

        var items = player.Creature.Powers
            .Select(p => $"{p.Id.Entry}={p.Amount}")
            .ToList();

        return items.Count == 0 ? "none" : string.Join(",", items);
    }

    /// <summary>本地玩家的血量 / 格挡（"hp 68/80 block 5"）。</summary>
    internal static string DescribeVitals()
    {
        var creature = LocalPlayer?.Creature;
        return creature is null
            ? "none"
            : $"hp {creature.CurrentHp}/{creature.MaxHp} block {creature.Block}";
    }
}
