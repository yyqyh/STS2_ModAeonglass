using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 虚无被抽到时的<b>口径枚举</b>（once / recycle / nopenalty）。
/// </summary>
/// <remarks>
/// ① 职责：取值。
/// ② 调用时机：<c>IntentRules</c> 与结算。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal enum VoidDrawMode
{
    /// <summary>
    /// 抽到一次、惩戒一次、**进手牌占位**，回合结束（怪物行动后）消耗掉（默认）。
    /// </summary>
    /// <remarks>这就是原版 Void 的 Ethereal 语义：在手牌里生效 → 离手即消失 ⇒ 只生效一次、且挤掉一个手牌位。</remarks>
    RemoveAfterFirstDraw,

    /// <summary>每次抽到都惩戒（污染变持续，强度大涨）。</summary>
    RecycleEachDraw,

    /// <summary>不再惩戒，只占一个手牌位（挡一次出牌位）。</summary>
    NoPenaltyRecycle,
}
