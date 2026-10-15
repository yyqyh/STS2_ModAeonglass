using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>「本回合换回时别丢手牌」的开关位。</summary>
/// <remarks>
/// ① 职责：<c>Arm</c> 挂"间隙至少要有几张 + 是否额外抽"的门槛；查询给 <c>GapRecallActions</c>。
/// ② 挂点与时机：卡打出时挂上；真正生效点是 <c>GapRecallActions.Recall</c> —— 它此刻才数间隙里有几张。
/// ③ 依赖：<c>Player</c> + 战斗 / 回合号。
/// ④ ⚠️ 本回合限定：回合 / 战斗一变自动失效（记账方式与 <c>GapAlternation</c> 一致）。
/// </remarks>
internal static class GapRecallGuard
{
    private static readonly Dictionary<Player, (MegaCrit.Sts2.Core.Combat.CombatState? Combat, int Turn, int Threshold, int DrawOnReturn)>
        Armed = [];

    /// <summary>挂上保护：本回合回归时，间隙牌数 ≥ <paramref name="threshold" /> 就不丢手牌。</summary>
    public static void Arm(Player player, int threshold)
    {
        var draw = Armed.TryGetValue(player, out var old) && ReferenceEquals(old.Combat, CurrentCombat) ? old.DrawOnReturn : 0;
        Armed[player] = (CurrentCombat, TurnOf(player), Math.Max(1, threshold), draw);
        Const.Logger.Info($"[{Const.ModId}] 缓收：本回合回归时若间隙 ≥{threshold} 张则不丢手牌");
    }

    /// <summary>附加项：回归成功后再抽几张（缓收的升级档用）。</summary>
    public static void ArmDrawOnReturn(Player player, int count)
    {
        if (!Armed.TryGetValue(player, out var armed))
        {
            return;
        }

        Armed[player] = (armed.Combat, armed.Turn, armed.Threshold, Math.Max(0, count));
        Const.Logger.Info($"[{Const.ModId}] 缓收：回归后再抽 {count} 张");
    }

    /// <summary>这一下回归要不要跳过"丢手牌"（以及回归后抽几张）。</summary>
    public static (bool KeepHand, int DrawAfter) Evaluate(Player player, int gapCards)
    {
        if (!Armed.TryGetValue(player, out var armed)
            || !ReferenceEquals(armed.Combat, CurrentCombat)
            || armed.Turn != TurnOf(player)
            || gapCards < armed.Threshold)
        {
            return (false, 0);
        }

        return (true, armed.DrawOnReturn);
    }

    /// <summary>清掉（调试 / 换 run）。</summary>
    public static void Clear() => Armed.Clear();

    private static MegaCrit.Sts2.Core.Combat.CombatState? CurrentCombat =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState();

    private static int TurnOf(Player player) => player.PlayerCombatState?.TurnNumber ?? -1;
}
