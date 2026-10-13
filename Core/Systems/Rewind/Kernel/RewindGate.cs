namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>单项闸门结果。</summary>
public readonly record struct GateCheck(string Name, bool Ok, string Reason)
{
    public string Display => Ok ? $"ok {Name}" : $"x  {Name}：{Reason}";
}

/// <summary>整组闸门结果。</summary>
public readonly record struct GateResult(bool Ok, IReadOnlyList<GateCheck> Checks)
{
    public string FirstFailureName =>
        Checks.FirstOrDefault(c => !c.Ok).Name ?? string.Empty;

    public string FirstFailureReason =>
        Checks.Where(c => !c.Ok).Select(c => c.Reason).FirstOrDefault() ?? string.Empty;
}

/// <summary>单项闸门。加一条判据 = 加一个实现并注册，不改其它闸门。</summary>
public interface IRewindGateCheck
{
    string Name { get; }

    GateCheck Check();
}

/// <summary>闸门组：按注册顺序求值，全部通过才算可回档。</summary>
public sealed class RewindGateGroup
{
    private readonly List<IRewindGateCheck> _checks = [];

    public IReadOnlyList<IRewindGateCheck> Checks => _checks;

    public void Add(IRewindGateCheck check) => _checks.Add(check);

    public GateResult Evaluate()
    {
        var results = new List<GateCheck>(_checks.Count);
        foreach (var check in _checks)
        {
            GateCheck result;
            try
            {
                result = check.Check();
            }
            catch (Exception ex)
            {
                result = new GateCheck(check.Name, false, ex.GetType().Name);
            }

            results.Add(result);
        }

        return new GateResult(results.All(r => r.Ok), results);
    }
}
