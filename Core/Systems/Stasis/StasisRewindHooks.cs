using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>
/// 搁浅 ↔ 回溯的<b>接缝外壳</b>：把区域读写交给回溯的 <c>StasisRewindExtension</c>。
/// </summary>
/// <remarks>
/// ① 职责：暴露搁浅条的抓取/恢复口。
/// ② 调用时机：回溯各阶段。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class StasisRewindHooks
{
    /// <summary>一条滞留记录：哪张牌 + 当时的倒计时。</summary>
    public sealed record Entry(CardModel Master, int CombatsLeft);

    /// <summary>快照：把搁浅当前的内容与计数记下来。</summary>
    public static Entry[] Capture(Player player)
        => StasisStore.ZoneCards(player)
            .Where(card => card is IStasisCard)
            .Select(card => new Entry(card, ((IStasisCard)card).CombatsLeft))
            .ToArray();

    /// <summary>恢复前：把「快照之后才进搁浅」的牌摘掉，并把母本送回牌组。</summary>
    /// <remarks>送回牌组这一步不能省 —— 否则这些牌会从牌组与搁浅里同时消失（丢牌）。</remarks>
    public static async Task BeforeRestore(Player player, Entry[] snapshot)
    {
        var known = snapshot.Select(entry => entry.Master).ToHashSet();

        foreach (var card in StasisStore.ZoneCards(player).ToList())
        {
            if (known.Contains(card))
            {
                continue;
            }

            Const.Logger.Info($"[{Const.ModId}] [搁浅] 回溯：摘掉快照之后进搁浅的 {card.Id.Entry}（母本回牌组）");
            await StasisReturn.ReturnToDeck(player, card);
        }
    }

    /// <summary>恢复后：把快照里的滞留卡放回搁浅，并把倒计时复位。</summary>
    public static void AfterRestore(Player player, Entry[] snapshot)
    {
        var zone = StasisStore.Zone(player);
        if (zone is null)
        {
            return;
        }

        foreach (var entry in snapshot)
        {
            if (entry.Master is IStasisCard stasis)
            {
                stasis.CombatsLeft = entry.CombatsLeft;
            }

            if (entry.Master.Pile is null && !zone.Cards.Contains(entry.Master))
            {
                zone.AddInternal(entry.Master, -1, true);
            }
        }

        Const.Logger.Info($"[{Const.ModId}] [搁浅] 回溯：搁浅已复位（{snapshot.Length} 张滞留卡）");
    }
}
