using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Gap;
namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>
/// 回溯 ↔ 时空裂隙的接缝（<b>order 200</b>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>约定：时空裂隙 只在 <c>OnCapture</c> 记数、两个 restore 刻意空实现</b> ——
/// "本体被快照还原 + 间隙副本仍在"是**刻意的策略收益**（印牌定价的收益对象），不是要修的 bug。
/// </para>
/// <para>
/// 注意间隙牌堆是 <c>RunPersistent</c>，**不在**战斗牌堆的恢复范围内，
/// 所以引擎恢复牌堆时天然不会碰它 —— 这两个空实现是"把设计写进代码"的显式声明。
/// </para>
/// </remarks>
internal sealed class GapRewindExtension : IRewindExtension
{
    private const string DraftKey = "m2.timeGap";

    public string Name => "aeonglass-m2-gap";

    public int Order => 200;

    public void OnCapture(ISnapshotDraft draft)
    {
        var perPlayer = new Dictionary<ulong, int>();
        foreach (var player in BridgeAccess.Players())
        {
            perPlayer[player.NetId] = GapStore.CountOf(player);
        }

        draft.Put(DraftKey, perPlayer);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Gap} 回溯 capture（只存副本，不回档）："
            + $"{string.Join(",", perPlayer.Select(kv => $"{kv.Key}={kv.Value}"))} | "
            + string.Join(" / ", BridgeAccess.Players().Select(GapStore.Describe)));
    }

    public void OnBeforeRestore(RewindContext ctx)
    {
        // 刻意空实现：时空裂隙 不回档。
    }

    public void OnAfterRestore(RewindContext ctx)
    {
        var captured = ctx.Parts.GetValueOrDefault(DraftKey) as Dictionary<ulong, int>;
        var now = string.Join(",", BridgeAccess.Players().Select(p => $"{p.NetId}={GapStore.CountOf(p)}"));

        // 接缝 3（Step 5）：回档结束后**同时刷新"间隙 UI"**。
        // 引擎的 HandUiSync 只重建手牌，间歇牌的牌堆 UI 不会自己知道模型变了 ⇒ 这里补一次内容通知，
        // 否则会出现"模型回档了、间隙界面还显示旧内容"。
        foreach (var player in BridgeAccess.Players())
        {
            GapStore.Zone(player)?.InvokeContentsChanged();
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Gap} 回溯 afterRestore：时空裂隙 不回档"
            + $"（快照时 {Describe(captured)}，现在 {now}）—— 印牌收益保留；间隙 UI 已同步刷新");
    }

    private static string Describe(Dictionary<ulong, int>? captured) =>
        captured is null ? "?" : string.Join(",", captured.Select(kv => $"{kv.Key}={kv.Value}"));
}
