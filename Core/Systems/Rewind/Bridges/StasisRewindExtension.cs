using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>M7 搁浅在 Rewind 快照里的条目。</summary>
internal sealed record ZoneEntry(ulong NetId, CardModel Card, int CombatsLeft);

/// <summary>
/// M4 回溯 ↔ M7 滞留的接缝（<b>order 100</b>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>2026-10-14 用户口径：搁浅不能回溯</b> —— 搁浅区完全随 run 走，回溯<b>不撤销</b>任何一次滞留。
/// </para>
/// <para>
/// 为什么不能简单"什么都不做"：回溯会把<b>牌组</b>恢复成上一回合的样子，
/// 快照之后才被搁浅的那张母本会<b>又出现在牌组里</b>（它在搁浅里也有一份），等于凭空多一张。
/// 所以 <c>OnAfterRestore</c> 要做的是"<b>重新落实离队</b>"：把还在搁浅里的牌从被恢复的牌堆里再摘掉一次，
/// 而不是把搁浅撤销掉。
/// </para>
/// <para>
/// <b>数据存在 <see cref="ISnapshotDraft"/> 里</b>（随快照走）—— 现在只用于日志与核对；
/// 真正的搁浅状态以 <c>StasisZone</c> 的当前内容为准。
/// </para>
/// <para>
/// 全部用 <c>CardPile.RemoveInternal/AddInternal</c>（同步、静默），不走 <c>CardPileCmd</c>：
/// <c>CardPileCmd.Add</c> 对 <c>HasBeenRemovedFromState == true</c> 的卡会静默失败。
/// </para>
/// </remarks>
internal sealed class StasisRewindExtension : IRewindExtension
{
    private const string DraftKey = "m7.emptyZone";

    public string Name => "aeonglass-m7-stasis";

    public int Order => 100;

    public void OnCapture(ISnapshotDraft draft)
    {
        var entries = CaptureEntries();
        draft.Put(DraftKey, entries);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 回溯 capture：搁浅 {entries.Count} 张 "
            + $"[{string.Join(",", entries.Select(e => $"{e.Card.Id.Entry}@{e.CombatsLeft}"))}]");
    }

    public void OnBeforeRestore(RewindContext ctx)
    {
        // 刻意空实现：搁浅不回档（用户 2026-10-14），恢复前的搁浅内容原样保留。
    }

    public void OnAfterRestore(RewindContext ctx)
    {
        var captured = EntriesOf(ctx);
        var reasserted = 0;

        foreach (var player in BridgeAccess.Players())
        {
            if (StasisZone.Zone(player) is not { } zone)
            {
                continue;
            }

            foreach (var card in StasisZone.ZoneCards(player).ToList())
            {
                // ① 引擎刚把牌组/牌堆恢复过，搁浅里这张可能又被放回了某个牌堆：重新落实"离队"。
                if (card.Pile is { } pile && pile.Type != zone.Type)
                {
                    pile.RemoveInternal(card, true);
                    reasserted++;
                }

                // ② 万一它连着搁浅一起被恢复动作摘掉了，放回搁浅（倒计时不动：搁浅不回档）。
                if (card.Pile is null && !zone.Cards.Contains(card))
                {
                    zone.AddInternal(card, -1, true);
                }
            }

            Core.Presentation.UI.CombatUiRefresh.RefreshAll(player);
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 回溯 afterRestore：搁浅不回档｜重新落实离队 {reasserted} 张"
            + $"｜快照拍下时有 {captured.Count} 张（现在按当前内容保留）");
    }

    private static List<ZoneEntry> EntriesOf(RewindContext ctx) =>
        ctx.Parts.GetValueOrDefault(DraftKey) as List<ZoneEntry> ?? [];

    /// <summary>母本放回跑局牌组（静默 <c>AddInternal</c>，理由见类注释）。</summary>
    private static void ReturnMasterToDeck(Player player, CardModel master)
    {
        var deck = CardPile.Get(PileType.Deck, player);
        if (deck is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Stasis} 撤销滞留时找不到牌组牌堆：{master.Id.Entry}");
            return;
        }

        if (deck.Cards.Contains(master))
        {
            return;
        }

        master.HasBeenRemovedFromState = false;
        deck.AddInternal(master, -1, true);

        // ⚠️ 这条是**静默**写入口（不走 CardPileCmd）⇒ 牌组数量与牌堆界面不会自己刷新。
        // 用户 2026-10-12：「卡组数量要在…滞留回归后刷新」；这里必须显式补一次（实测 log 里"撤销滞留"之后数量是旧的）。
        Core.Presentation.UI.CombatUiRefresh.RefreshAll(player);

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 撤销滞留：母本 {master.Id.Entry} 已放回牌组");
    }

    private static List<ZoneEntry> CaptureEntries()
    {
        var entries = new List<ZoneEntry>();
        foreach (var player in BridgeAccess.Players())
        {
            foreach (var card in StasisZone.ZoneCards(player))
            {
                entries.Add(new ZoneEntry(
                    player.NetId,
                    card,
                    card is IStasisCard stasis ? stasis.CombatsLeft : 0));
            }
        }

        return entries;
    }
}
