using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace STS2_Aeonglass.Core.Content.Relics;

/// <summary>主动刷新遗物角标：战斗结束 / 换副 / 回溯这类不经过 <c>DisplayAmountChanged</c> 的路径。</summary>
internal static class AeonglassRelicBadges
{
    public static void Refresh(Player player)
    {
        foreach (var relic in player.Relics)
        {
            switch (relic)
            {
                case AeonglassCore core:
                    core.NotifyBadgeChanged();
                    break;
                case AeonglassBrokenWatch watch:
                    watch.NotifyBadgeChanged();
                    break;
            }
        }
    }

    public static void Refresh(IRunState runState)
    {
        foreach (var player in runState.Players)
        {
            Refresh(player);
        }
    }
}
