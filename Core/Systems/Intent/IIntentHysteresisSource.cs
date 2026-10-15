namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 敌人侧迟滞的<b>额度来源</b>（静域）：每回合给怪物手牌第一张意图打迟滞。
/// </summary>
/// <remarks>
/// ① 职责：提供额度与消耗。
/// ② 调用时机：回合准备。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal interface IIntentHysteresisSource
{
    /// <summary>本次最多能打几张迟滞（0 = 不介入；调用方只会消费"真的标记成功"的数量）。</summary>
    int PendingIntentHysteresis { get; }

    /// <summary>结算：实际用掉 <paramref name="used" /> 张额度（实现方自己扣）。</summary>
    void ConsumeIntentHysteresis(int used);
}
