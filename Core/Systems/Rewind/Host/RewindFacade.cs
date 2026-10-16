using Godot;

using STS2_Aeonglass.Core.Systems;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;
using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Rewind.Host;

/// <summary>回溯的对外唯一门面：UI 与热键只认识它，引擎细节（Kernel / Graph / StateCapture）不对外暴露。</summary>
/// <remarks>
/// ① 职责：闸门评估 <c>EvaluateGates</c>、战斗开始 / 结束与回合开始的抓拍、<c>CaptureNow</c>；回档执行在 <c>RewindFacade.Runner.cs</c> 分册。
/// ② 挂点与时机：<c>RewindBootstrapper</c> 与宿主补丁在对应时机调（战斗开始 / 回合开始 / 战斗结束 / 手动请求）。
/// ③ 依赖：<c>Rewind/{Kernel,StateCapture,Graph,Policy}</c>、<c>EndOfCombatSequencer</c>、设置项。
/// ④ ⚠️ 移植到别的 mod 时只需要改这个文件的装配部分；快照不跨会话。
/// </remarks>
internal sealed partial class RewindFacade
{
    public static RewindFacade Instance { get; } = new();

    private readonly RewindTransaction _transaction;
    private bool _lastRewindSucceeded;

    private RewindFacade()
    {
        Log = new RewindLog(
            capacity: 200,
            // 2026-10-16：宿主 sink 补上机制前缀 —— 之前 `rewind lab ready / capture #N / combat started|ended`
            // 这些行没有 `[回溯]`（lint 抓不到，因为前缀该加在 sink 这一层），实机排查时不好按机制过滤。
            sink: line => MegaCrit.Sts2.Core.Logging.Log.Info($"[{Const.ModId}] {Const.Prefix.Rewind} {line}"));

        Ring = new SnapshotRing(capacity: 5);
        State = new RewindState();
        Extensions = new ExtensionRegistry(Log);
        Scope = RewindScope.Default;
        TurnScope = TurnScope.Default;

        _transaction = new RewindTransaction(
            new CombatRewindLock(Log),
            new RewindValidator(),
            Log,
            isStable: () =>
                CombatAccess.IsRuntimeSettled(out _)
                && (!Scope.VfxBarrierStrict || !VfxBarrier.IsBusy),
            waitFrame: WaitFrameAsync);

        // 闸门顺序即面板显示顺序；加一条判据 = 加一行。
        Gates.Add(new SingleplayerGate());
        Gates.Add(new InCombatGate());
        Gates.Add(new PlayPhaseGate());
        Gates.Add(new PlayerActionsGate());
        Gates.Add(new NotReadyGate());
        Gates.Add(new RuntimeSettledGate());
        Gates.Add(new NoSelectionGate());
        Gates.Add(new NoVfxInFlightGate(() => Scope.VfxBarrierStrict));
    }

    public IRewindLog Log { get; }

    public SnapshotRing Ring { get; }

    public RewindState State { get; }

    public RewindGateGroup Gates { get; } = new();

    public ExtensionRegistry Extensions { get; }

    public RewindScope Scope { get; }

    public TurnScope TurnScope { get; }

    /// <summary>最近一次拍摄 / 回档的结果，面板直接显示。</summary>
    public string LastResult { get; private set; } = "-";

    public bool RewindInProgress { get; private set; }

    /// <param name="forCapture">
    /// <c>true</c> = 这次评估是给<b>回合开始打点</b>用的 ⇒ 跳过"只对回档成立"的闸门
    /// （见 <c>IRewindGateCheck.AppliesToCapture</c>）；回档侧一律用默认的 <c>false</c>，口径不变。
    /// </param>
    public GateResult EvaluateGates(bool forCapture = false) => Gates.Evaluate(forCapture);

    public void OnCombatEnded()
    {
        Ring.Clear();
        State.ToIdle();
        LastResult = "战斗结束，快照已清空";
        Log.Info("combat ended, snapshots cleared");
    }

    public void OnCombatStarted()
    {
        Log.Info("combat started");
    }

    /// <summary>回合开始的自动打点。</summary>
    public void CaptureTurnStart()
    {
        if (TurnScope.CaptureOnTurnStart)
        {
            CaptureNow("turn");
        }
    }

    /// <summary>拍一份快照。闸门不过就直接放弃（不做半成品快照）。</summary>
    public ISnapshot? CaptureNow(string label)
    {
        // ⚠️ 抓拍是**跑在本体流程里**的（回合开始的同步回调直接调到这里）⇒ 整流包兜底：
        //    抓拍失败只等于"这一回合没有快照"（回溯会报"没有上一回合快照"），
        //    绝不能让异常穿进本体回合循环（2026-10-16 实测：异常一路冒到 CombatManager.StartTurn，
        //    日志 "turn loop died … the combat is stuck"，战斗卡死、回合结束按键不出现）。
        try
        {
            // 打点专用评估：跳过"VFX 屏障"这类只对回档成立的闸门（它会把每次卡面特效在飞的打点全挡掉）。
            var gate = EvaluateGates(forCapture: true);
            if (!gate.Ok)
            {
                Log.Info($"capture skipped: {gate.FirstFailureName} - {gate.FirstFailureReason}");
                return null;
            }

            var turn = CombatAccess.CurrentTurnNumber();
            var id = Ring.NextId(turn, label);
            var draft = new SnapshotDraft(id);
            Extensions.Capture(draft);

            var snapshot = CombatStateSnapshot.Capture(id, Scope);
            if (snapshot is null)
            {
                Log.Warn("capture skipped: no local player");
                return null;
            }

            // 扩展草稿必须随这份快照一起带着 —— 否则恢复时拿不到"当时"的 搁浅 搁浅内容 / 时空裂隙副本。
            var aware = new ExtensionAwareSnapshot(snapshot, Extensions, draft.Parts);
            Ring.Add(aware);
            LastResult = $"拍摄 #{id.Index} @ T{turn}";
            Log.Info($"capture {id} | {snapshot.Summary} | powers[{CombatAccess.DescribePowers()}]");
            return aware;
        }
        catch (Exception ex)
        {
            LastResult = $"拍摄失败（已降级跳过）：{ex.GetType().Name}";
            Log.Error($"capture failed（已降级跳过，本回合没有快照）：{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

}
