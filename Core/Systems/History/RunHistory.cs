using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Content.Relics;

namespace STS2_Aeonglass.Core.Systems.History;

/// <summary>
/// <b>本局（跨战斗）历史</b>：现在只有一条 —— 累计回溯次数（v2「因果」读它）。
/// </summary>
/// <remarks>
/// <para>
/// 与 <see cref="CombatHistory" /> 的分工：那个是"本场战斗"的账（每场战斗自动清零），
/// 这个是"整局"的账 —— 存<b>起始遗物</b>的 <c>[SavedProperty]</c> 上（随存档走），
/// 所以战斗结束、读档都不会丢。
/// </para>
/// <para>写入点唯一：<c>RewindModule</c> 里"回溯真的成立"的那一处（与 <see cref="CombatHistory.RecordRewind" /> 并排）。</para>
/// </remarks>
internal static class RunHistory
{
    /// <summary>本局累计回溯过几次（拿不到遗物就返回 0）。</summary>
    public static int Rewinds(Player player) => RelicOf(player)?.RewindsThisRun ?? 0;

    /// <summary>记一次成功回溯（跨战斗累计）。</summary>
    public static void RecordRewind(Player player)
    {
        if (RelicOf(player) is { } relic)
        {
            relic.RewindsThisRun++;
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 本局累计回溯 = {relic.RewindsThisRun}");
        }
    }

    /// <summary>起始遗物（沙漏之心）；拿不到（别人选的角色 / 还没发遗物）返回 null。</summary>
    private static AeonglassCore? RelicOf(Player player) => player.Relics.OfType<AeonglassCore>().FirstOrDefault();
}
