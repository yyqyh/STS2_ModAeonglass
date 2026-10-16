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

    /// <summary>
    /// 这条闸门要不要参与<b>回合开始打点</b>（抓快照）的评估。
    /// </summary>
    /// <remarks>
    /// 默认 <c>true</c>（打点与回档用同一套判据）。只有"只对<b>回档</b>成立"的闸门才覆写成 <c>false</c> ——
    /// 例：<c>NoVfxInFlightGate</c>（卡面特效在飞时不许回档），打点是只读的、动画不影响模型快照。
    /// 2026-10-16 实测：它会把回合开始的打点一起挡掉（一局 5 次打点只成 2 次）⇒ 回溯目标会退化成更早的回合。
    /// </remarks>
    bool AppliesToCapture => true;

    GateCheck Check();
}
