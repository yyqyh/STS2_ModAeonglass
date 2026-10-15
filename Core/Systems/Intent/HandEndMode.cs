using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 回合结束未打出的意图<b>去向枚举</b>。
/// </summary>
/// <remarks>
/// ① 职责：取值（弃牌堆 / 回堆底 / 洗回）。
/// ② 调用时机：<c>IntentHandCycle</c>。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal enum HandEndMode
{
    /// <summary>一起进弃牌堆（默认）⇒ 三堆循环闭环。</summary>
    ToDiscard,

    /// <summary>确定性回抽牌堆底（Lab C 旧行为，不消耗 RNG）。</summary>
    ToDeckBottom,

    /// <summary>随机洗回抽牌堆（消耗怪物 RNG）。</summary>
    ToDeckShuffle,
}
