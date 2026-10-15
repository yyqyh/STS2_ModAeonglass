using STS2_Aeonglass.Core.Systems.Deferral;
using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>
/// M4 回溯 ↔「延宕」的接缝（<b>order 250</b>）。
/// </summary>
/// <remarks>
/// 规则层 §2.8.4 第 6 条：延宕与欠账都是<b>玩家侧状态</b> ⇒ 回溯要一并回退（合法且合理）。
/// 所以这条接缝是**真的回档**（不是像 M2 间隙那样的刻意空实现）：把欠账列表与延宕窗口一起写回去。
/// </remarks>
internal sealed class DeferralRewindExtension : IRewindExtension
{
    private const string DraftKey = "aeonglass.deferral";

    public string Name => "aeonglass-deferral";

    public int Order => 250;

    public void OnCapture(ISnapshotDraft draft)
    {
        var snapshot = DeferralLedger.Snapshot();
        draft.Put(DraftKey, snapshot);
        Const.Logger.Info(
            $"[{Const.ModId}] 延宕：回溯 capture（欠账 / 窗口已入快照）"
            + $"｜{string.Join(" / ", Players().Select(DeferralLedger.Describe))}");
    }

    public void OnBeforeRestore(RewindContext ctx)
    {
        // 恢复前不做额外动作：能力本身会被引擎按快照还原，账本在 afterRestore 里对准。
    }

    public void OnAfterRestore(RewindContext ctx)
    {
        if (ctx.Parts.GetValueOrDefault(DraftKey) is not Dictionary<ulong, (List<(int DueTurn, decimal Amount, string Source)> Debts, List<(int ArmedTurn, int DueTurn, int Turns)> Arms)> snapshot)
        {
            Const.Logger.Warn($"[{Const.ModId}] 延宕：回溯 afterRestore 拿不到片段 ⇒ 本次不回档（只记日志）");
            return;
        }

        DeferralLedger.Restore(snapshot);
        Const.Logger.Info(
            $"[{Const.ModId}] 延宕：回溯 afterRestore（欠账 / 窗口已回退）"
            + $"｜{string.Join(" / ", Players().Select(DeferralLedger.Describe))}");
    }

    private static IReadOnlyList<MegaCrit.Sts2.Core.Entities.Players.Player> Players() =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()?.Players ?? [];
}
