namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 门禁检查的<b>结果</b>（通过与否 + 理由）。
/// </summary>
/// <remarks>
/// ① 职责：结果载体。
/// ② 调用时机：门禁评估时构造。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public readonly record struct GateCheck(string Name, bool Ok, string Reason)
{
    public string Display => Ok ? $"ok {Name}" : $"x  {Name}：{Reason}";
}
