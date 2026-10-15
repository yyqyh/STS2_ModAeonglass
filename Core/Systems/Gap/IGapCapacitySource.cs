using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>「间隙容量 +N」的能力读口（结构成长类卡都走它）。</summary>
/// <remarks>
/// ① 职责：声明 <c>GapCapacityBonus</c>（以及双结构能力同时要给的"手牌上限 +N"）。
/// ② 挂点与时机：间隙算容量时遍历该玩家的能力求和。
/// ③ 依赖：无（实现方是能力）。
/// ④ ⚠️ 为什么用接口而不认具体类：摆幅 / 永世 这类"双结构"能力必须同时给出"手牌上限 +N"与"间隙容量 +M"，
/// 若把容量加成写死在具体能力类里，它们就得再挂一个纯容量能力（一张牌两个状态图标）。
/// 口径与搁浅通知一致：能力在场即生效、离场即失效（读时求和，不做加减记账）。
/// </remarks>
internal interface IGapCapacitySource
{
    /// <summary>这个能力给该玩家加多少间隙容量（可以是 0）。</summary>
    int GapCapacityBonus { get; }
}
