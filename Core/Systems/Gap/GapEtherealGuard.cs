using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>「本回合坍缩进去的副本不带虚无」的开关位。</summary>
/// <remarks>
/// ① 职责：<c>Arm</c> 挂单 + 查询本回合是否生效。
/// ② 挂点与时机：卡打出时挂上；生效点在 <c>GapRecallActions.Recall</c>（它才给副本挂「虚无」）—— 本回合换回时跳过那一步。
/// ③ 依赖：<c>Player</c> + 战斗 / 回合号。
/// ④ ⚠️ 本回合限定：回合 / 战斗一变自动失效（记账方式与 <c>GapAlternation</c> 一致）。
/// </remarks>
internal static class GapEtherealGuard
{
    private static readonly Dictionary<Player, (MegaCrit.Sts2.Core.Combat.CombatState? Combat, int Turn)> Armed = [];

    /// <summary>本回合的副本不带虚无。</summary>
    public static void Arm(Player player)
    {
        Armed[player] = (CurrentCombat, TurnOf(player));
        Const.Logger.Info($"[{Const.ModId}] 存真：本回合回归的副本不带虚无");
    }

    /// <summary>现在这一下回归要不要跳过"给副本挂虚无"？</summary>
    public static bool Active(Player player)
        => Armed.TryGetValue(player, out var armed)
           && ReferenceEquals(armed.Combat, CurrentCombat)
           && armed.Turn == TurnOf(player);

    /// <summary>清掉（调试 / 换 run）。</summary>
    public static void Clear() => Armed.Clear();

    private static MegaCrit.Sts2.Core.Combat.CombatState? CurrentCombat =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState();

    private static int TurnOf(Player player) => player.PlayerCombatState?.TurnNumber ?? -1;
}
