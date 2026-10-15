namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>
/// 标记接口：<b>这张卡打出后进搁浅</b>。
/// </summary>
/// <remarks>
/// ① 职责：类型标记 + 倒计数字段约定。
/// ② 调用时机：卡牌落点与搁浅流程。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal interface IStasisCard
{
    /// <summary>还剩几次"战斗结束"才归队（内部按 <c>X + 1</c> 存，所以打出当场那次不计）。</summary>
    int CombatsLeft { get; set; }
}
