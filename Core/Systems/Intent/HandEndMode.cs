using STS2_Aeonglass.Core.Config;


namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>回合结束时未打出意图卡的去向（新三堆模型的"待确认 2"）。</summary>
internal enum HandEndMode
{
    /// <summary>一起进弃牌堆（默认）⇒ 三堆循环闭环。</summary>
    ToDiscard,

    /// <summary>确定性回抽牌堆底（Lab C 旧行为，不消耗 RNG）。</summary>
    ToDeckBottom,

    /// <summary>随机洗回抽牌堆（消耗怪物 RNG）。</summary>
    ToDeckShuffle,
}
