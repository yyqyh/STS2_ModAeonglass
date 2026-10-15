using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>时空裂隙 × 回溯的<b>接缝（只留位，未接线）</b>——`GapRewindExtension` 的等价物，order 200。</summary>
/// <remarks>
/// <para>
/// <b>时空裂隙 刻意不回档</b>：这是定价表 §0 承认的策略收益 —— 恢复战斗牌堆后本体回来，
/// 而间隙与副本不动 ⇒ 净多出"副本那一份"。所以这里的 <c>BeforeRestore</c> / <c>AfterRestore</c>
/// **刻意空实现**，只有 <c>Capture</c> 记下"快照那一刻间隙里有什么"（留给以后观察 / 定价统计用）。
/// </para>
/// <para>回溯 落地时的接线（照 Lab B）：用 <c>IRewindExtension</c>（order 200）包一层接到三个回调；
/// 必须同时带上 <c>ExtensionAwareSnapshot</c>（RewindLab 原版把 draft 丢掉、且从不调
/// <c>OnBeforeRestore</c>/<c>OnAfterRestore</c>，只注册 = 死代码）；扩展数据要随 <c>ISnapshotDraft</c> 走。</para>
/// <para>与 搁浅的 <see cref="StasisRewindHooks" />（order 100）互不依赖：搁浅 撤销要"母本回牌组"，时空裂隙 明确不回滚。</para>
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
