namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>门禁</b>：把若干"能不能回溯"的检查串成一组，返回第一条拒绝理由。
/// </summary>
/// <remarks>
/// ① 职责：<c>IRewindGateCheck</c> / <c>GateCheck</c> / <c>RewindGateGroup</c>（三条各一文件）。
/// ② 调用时机：发起回溯前（<c>RewindFacade.EvaluateGates</c>）。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public readonly record struct GateResult(bool Ok, IReadOnlyList<GateCheck> Checks)
{
    public string FirstFailureName =>
        Checks.FirstOrDefault(c => !c.Ok).Name ?? string.Empty;

    public string FirstFailureReason =>
        Checks.Where(c => !c.Ok).Select(c => c.Reason).FirstOrDefault() ?? string.Empty;
}

/// <summary>闸门组：按注册顺序求值，全部通过才算可回档。</summary>
public sealed class RewindGateGroup
{
    private readonly List<IRewindGateCheck> _checks = [];

    public IReadOnlyList<IRewindGateCheck> Checks => _checks;

    public void Add(IRewindGateCheck check) => _checks.Add(check);

    /// <param name="forCapture">
    /// <c>true</c> = 这次是<b>回合开始打点</b>（抓快照）而不是回档 ⇒ 跳过"只对回档成立"的闸门
    /// （见 <see cref="IRewindGateCheck.AppliesToCapture" />）。
    /// </param>
    public GateResult Evaluate(bool forCapture = false)
    {
        var results = new List<GateCheck>(_checks.Count);
        foreach (var check in _checks)
        {
            if (forCapture && !check.AppliesToCapture)
            {
                continue;
            }

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
