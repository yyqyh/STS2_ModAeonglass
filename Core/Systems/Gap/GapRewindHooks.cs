using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>时空裂隙 × 回溯的<b>接缝</b>：order 200，只留位、刻意不回档。</summary>
/// <remarks>
/// ① 职责：<c>OnCapture</c> 记下"快照那一刻间隙里有什么"（留给观察 / 定价统计）；<c>OnBeforeRestore</c> / <c>OnAfterRestore</c> 刻意空实现。
/// ② 挂点与时机：<c>IRewindExtension</c>，由回溯模块注册；只在这条接缝上被调。
/// ③ 依赖：<c>ISnapshotDraft</c>、<c>GapStore</c>；<c>GapRewindExtension</c>（Lab B 版）是等价物。
/// ④ ⚠️ <b>时空裂隙刻意不回档</b>—— 定价表 §0 承认的策略收益：恢复战斗牌堆后本体回来、间隙与副本不动 ⇒ 净多出"副本那一份"。
/// 真正接线时要用 <c>IRewindExtension</c>（order 200）包一层，并同时带上 <c>ExtensionAwareSnapshot</c>
/// （RewindLab 原版把 draft 丢掉、且从不回调这两个方法，只注册 = 死代码）。与搁浅的 <c>StasisRewindHooks</c>（order 100）互不依赖。
/// </remarks>
internal static class GapRewindHooks
{
    /// <summary>快照载荷：那一刻间隙里的副本实例。</summary>
    /// <remarks>回溯 落地时要把它转成可进 <c>ISnapshotDraft</c> 的形式（纯数据 / id 列表 + 计数）。</remarks>
    public sealed record Entry(List<CardModel> Copies);

    /// <summary>只存副本 —— 不改变任何状态，纯读。</summary>
    public static Entry Capture(Player player)
        => new(GapStore.Zone(player)?.Cards.ToList() ?? []);

    /// <summary>
    /// 恢复前：<b>刻意空实现</b>。
    /// </summary>
    /// <remarks>时空裂隙 不回档是设计（策略收益），不是遗漏 —— 别在这里"顺手"把副本删掉。</remarks>
    public static void BeforeRestore(Player player, Entry snapshot)
    {
        Const.Logger.Info(
            $"[{Const.ModId}] [间隙] 回溯：时空裂隙 不回档（快照时 {snapshot.Copies.Count} 张，"
            + $"现在 {GapStore.CountOf(player)} 张）—— 印牌收益保留");
    }

    /// <summary>恢复后：<b>刻意空实现</b>（同上）。</summary>
    public static void AfterRestore(Player player, Entry snapshot)
    {
        // 有意为空：时空裂隙 不回档。
    }
}
