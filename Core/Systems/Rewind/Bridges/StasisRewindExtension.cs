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
/// <b>这是"必须回档"的一条</b>：只在 <c>OnCapture</c> 存数据、不撤销的话，
/// 快照之后才打出的滞留卡会**悬空**（母本已离队、搁浅里那张不在快照里），等于丢牌。
/// 所以 <c>OnBeforeRestore</c> 要把"快照之后才进搁浅"的牌撤销滞留（从搁浅删除 + 母本回牌组 + 清计数），
/// <c>OnAfterRestore</c> 再把快照里那张归位。
/// </para>
/// <para>
/// <b>数据存在 <see cref="ISnapshotDraft"/> 里（随快照走），不用实例字段缓存</b> ——
/// 回档事务会先拍一份"备份快照"，用实例字段会被那份备份覆盖，导致撤销作用于错误的时刻。
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
        var captured = EntriesOf(ctx);
        var known = captured.Select(e => e.Card).ToHashSet(ReferenceEqualityComparer.Instance);
        var undone = 0;

        foreach (var player in BridgeAccess.Players())
        {
            if (StasisZone.Zone(player) is not { } zone)
            {
                continue;
            }

            foreach (var card in zone.Cards.ToList())
            {
                if (known.Contains(card))
                {
                    continue;
                }

                // 快照之后才进的搁浅 ⇒ 撤销滞留：从搁浅删除 + 母本放回原牌组 + 清计数。
                zone.RemoveInternal(card, true);
                card.HasBeenRemovedFromState = false;
                ReturnMasterToDeck(player, card);
                undone++;
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 回溯 beforeRestore：撤销滞留 {undone} 张"
            + $"（快照里有 {captured.Count} 张）");
    }

    public void OnAfterRestore(RewindContext ctx)
    {
        var captured = EntriesOf(ctx);
        var restored = 0;

        foreach (var entry in captured)
        {
            var player = BridgeAccess.Players().FirstOrDefault(p => p.NetId == entry.NetId);
            if (player is null || StasisZone.Zone(player) is not { } zone)
            {
                continue;
            }

            // 如果这张牌在快照之后归了队，先把它从牌组摘回搁浅。
            if (entry.Card.Pile?.Type == PileType.Deck)
            {
                entry.Card.Pile.RemoveInternal(entry.Card, true);
            }

            if (entry.Card.Pile is null)
            {
                zone.AddInternal(entry.Card, -1, true);
            }

            if (entry.Card is IStasisCard stasis)
            {
                stasis.CombatsLeft = entry.CombatsLeft;
            }

            restored++;
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 回溯 afterRestore：搁浅归位 {restored} 张（计数已复位）");
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
