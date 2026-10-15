namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯门禁的<b>单个检查</b>接口。
/// </summary>
/// <remarks>
/// ① 职责：一个检查只回答"过 / 不过 + 理由"。
/// ② 调用时机：<c>RewindGateGroup</c> 逐个问。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public interface IRewindGateCheck
{
    string Name { get; }

    GateCheck Check();
}
