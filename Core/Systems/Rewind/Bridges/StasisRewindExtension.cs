using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

using STS2_Aeonglass.Core.Config;

using STS2_Aeonglass.Core.Systems.Stasis;

namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>回溯 ↔ 搁浅 的接缝（<b>order 100</b>）：搁浅不回档，只"重新落实离队"。</summary>
/// <remarks>
/// ① 职责：抓快照时记下搁浅条目；恢复后把仍在搁浅的牌从被恢复的牌堆里再摘掉一次。
/// ② 挂点与时机：<c>IRewindExtension</c>，由 <c>RewindModule</c> 注册；<c>OnBeforeRestore</c> 刻意空实现。
/// ③ 依赖：<c>StasisStore</c>/<c>IStasisCard</c>、<c>RewindContext</c>（草稿 key <c>stasis.emptyZone</c>）、<c>CombatUiRefresh</c>。
/// ④ ⚠️ 2026-10-14 用户口径「搁浅不能回溯」⇒ 恢复会把牌组退回快照态、快照之后才搁浅的母本会"又出现一份"，
/// 所以这里做的是重新摘掉它，而不是撤销搁浅。全用 <c>CardPile.RemoveInternal/AddInternal</c>（同步、静默）——
/// 不走 <c>CardPileCmd</c>（对 <c>HasBeenRemovedFromState == true</c> 的卡会静默失败）⇒ 改完必须手动刷 UI。
/// </remarks>
internal sealed class StasisRewindExtension : IRewindExtension
{
    private const string DraftKey = "stasis.emptyZone";

    public string Name => "aeonglass-stasis";

    public int Order => 100;

    public void OnCapture(ISnapshotDraft draft)
    {
        var entries = CaptureEntries();
        draft.Put(DraftKey, entries);

        // 每次快照都打 ⇒ 归「详细诊断日志」开关（默认关）。
        if (AeonglassSettingStore.DebugVerboseLogs)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Stasis} 回溯 capture：搁浅 {entries.Count} 张 "
                + $"[{string.Join(",", entries.Select(e => $"{e.Card.Id.Entry}@{e.CombatsLeft}"))}]");
        }
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
            if (StasisStore.Zone(player) is not { } zone)
            {
                continue;
            }

            foreach (var card in StasisStore.ZoneCards(player).ToList())
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

            Core.Presentation.Combat.CombatUiRefresh.RefreshAll(player);
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 回溯 afterRestore：搁浅不回档｜重新落实离队 {reasserted} 张"
            + $"｜快照拍下时有 {captured.Count} 张（现在按当前内容保留）");
    }

    private static List<ZoneEntry> EntriesOf(RewindContext ctx) =>
        ctx.Parts.GetValueOrDefault(DraftKey) as List<ZoneEntry> ?? [];

    private static List<ZoneEntry> CaptureEntries()
    {
        var entries = new List<ZoneEntry>();
        foreach (var player in BridgeAccess.Players())
        {
            foreach (var card in StasisStore.ZoneCards(player))
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
