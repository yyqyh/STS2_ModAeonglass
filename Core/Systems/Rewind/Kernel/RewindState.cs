namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>引擎状态机。面板只读它，不写它。</summary>
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
