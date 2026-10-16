using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Deferral;
using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>
/// 回溯 ↔ 延宕的接缝（order 250）：欠账与实例列表一起回退。
/// </summary>
/// <remarks>
/// ① 职责：<c>DeferralLedger.Snapshot/Restore</c>。
/// ② 调用时机：回溯抓取/恢复。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
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
        // 每次快照都打 ⇒ 归「详细诊断日志」开关（默认关）。
        if (AeonglassSettingStore.DebugVerboseLogs)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Deferral} 延宕：回溯 capture（欠账 / 窗口已入快照）"
                + $"｜{string.Join(" / ", Players().Select(DeferralLedger.Describe))}");
        }
    }

    public void OnBeforeRestore(RewindContext ctx)
    {
        // 恢复前不做额外动作：能力本身会被引擎按快照还原，账本在 afterRestore 里对准。
    }

    public void OnAfterRestore(RewindContext ctx)
    {
        if (ctx.Parts.GetValueOrDefault(DraftKey) is not Dictionary<ulong, (List<(int DueTurn, decimal Amount, string Source)> Debts, List<(int ArmedTurn, int DueTurn, int Turns)> Arms)> snapshot)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Deferral} 延宕：回溯 afterRestore 拿不到片段 ⇒ 本次不回档（只记日志）");
            return;
        }

        DeferralLedger.Restore(snapshot);
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Deferral} 延宕：回溯 afterRestore（欠账 / 窗口已回退）"
            + $"｜{string.Join(" / ", Players().Select(DeferralLedger.Describe))}");
    }

    private static IReadOnlyList<MegaCrit.Sts2.Core.Entities.Players.Player> Players() =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()?.Players ?? [];
}
