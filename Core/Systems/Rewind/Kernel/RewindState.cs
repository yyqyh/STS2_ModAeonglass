namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯引擎的<b>运行态</b>：当前阶段与门禁信息（内核只认它，不认游戏对象）。
/// </summary>
/// <remarks>
/// ① 职责：保存 <c>RewindPhase</c> 与本次回溯的门禁/快照引用。
/// ② 调用时机：引擎启动时创建，每次回溯更新。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public sealed class RewindState
{
    private RewindPhase _phase = RewindPhase.Idle;

    public event Action? Changed;

    public RewindPhase Phase
    {
        get => _phase;
        private set
        {
            if (_phase == value)
            {
                return;
            }

            _phase = value;
            Changed?.Invoke();
        }
    }

    public string Detail { get; private set; } = string.Empty;

    public void ToIdle(string detail = "") => Set(RewindPhase.Idle, detail);

    public void ToLocked(string detail) => Set(RewindPhase.Locked, detail);

    public void ToRestoring(string detail) => Set(RewindPhase.Restoring, detail);

    public void ToFailed(string detail) => Set(RewindPhase.Failed, detail);

    private void Set(RewindPhase phase, string detail)
    {
        Detail = detail;
        Phase = phase;
    }
}
