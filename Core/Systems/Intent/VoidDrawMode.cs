using STS2_Aeonglass.Core.Config;


namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>虚无在循环里被抽到时的口径（新三堆模型的"待确认 1"）。</summary>
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
