using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Content.Relics;

namespace STS2_Aeonglass.Core.Systems.History;

/// <summary>
/// 本局（跨战斗）历史：目前是累计回溯次数（存在起始遗物上）。
/// </summary>
/// <remarks>
/// ① 职责：累加与读取。
/// ② 调用时机：回溯成功时。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
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
