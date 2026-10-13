using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Zones;

/// <summary>M7 × M4 的<b>接缝（只留位，未接线）</b>。</summary>
/// <remarks>
/// <para>
/// 按 C 方案的策略「先做机制、M4 再 retrofit」：这一层现在就把三个动作的**语义**写清楚，
/// 但**没有任何人调用它**。M4 落地时的接线方式（照 Lab B）：
/// </para>
/// <list type="number">
/// <item><description>用 <c>IRewindExtension</c>（<b>order 100</b>）包一层，把 Capture / BeforeRestore / AfterRestore 分别接到 OnCapture / OnBeforeRestore / OnAfterRestore；</description></item>
/// <item><description>必须同时带上 <c>ExtensionAwareSnapshot</c> —— RewindLab 原版把 draft 丢掉、且从不调 OnBeforeRestore/OnAfterRestore，<b>只注册扩展等于死代码</b>；</description></item>
/// <item><description>扩展数据要随 <c>ISnapshotDraft</c> 走（存在实例字段上会被备份快照覆盖）。</description></item>
/// </list>
/// <para>
/// <b>唯一性铁律</b>：撤销滞留时必须把<b>母本放回牌组</b>。只把它从搁浅"摘掉"会让牌悬空 = 丢牌
/// （Lab A 版踩过，Lab B 修好，实测 <c>drift = 0/20</c>）。
/// </para>
/// </remarks>
internal static class StasisRewindSeam
{
    /// <summary>一条滞留记录：哪张牌 + 当时的倒计时。</summary>
    public sealed record Entry(CardModel Master, int CombatsLeft);

    /// <summary>快照：把搁浅当前的内容与计数记下来。</summary>
    public static Entry[] Capture(Player player)
        => StasisZone.ZoneCards(player)
            .Where(card => card is IStasisCard)
            .Select(card => new Entry(card, ((IStasisCard)card).CombatsLeft))
            .ToArray();

    /// <summary>恢复前：把「快照之后才进搁浅」的牌摘掉，并把母本送回牌组。</summary>
    /// <remarks>送回牌组这一步不能省 —— 否则这些牌会从牌组与搁浅里同时消失（丢牌）。</remarks>
    public static async Task BeforeRestore(Player player, Entry[] snapshot)
    {
        var known = snapshot.Select(entry => entry.Master).ToHashSet();

        foreach (var card in StasisZone.ZoneCards(player).ToList())
        {
            if (known.Contains(card))
            {
                continue;
            }

            Const.Logger.Info($"[{Const.ModId}] [M7] 回溯：摘掉快照之后进搁浅的 {card.Id.Entry}（母本回牌组）");
            await StasisZone.ReturnToDeck(player, card);
        }
    }

    /// <summary>恢复后：把快照里的滞留卡放回搁浅，并把倒计时复位。</summary>
    public static void AfterRestore(Player player, Entry[] snapshot)
    {
        var zone = StasisZone.Zone(player);
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

        Const.Logger.Info($"[{Const.ModId}] [M7] 回溯：搁浅已复位（{snapshot.Length} 张滞留卡）");
    }
}
