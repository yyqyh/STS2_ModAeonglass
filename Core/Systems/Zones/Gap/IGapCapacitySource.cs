using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Zones;

/// <summary>「间隙容量 +N」的能力读口（GRW 通道的结构成长卡都走它）。</summary>
/// <remarks>
/// <para>
/// <b>为什么要接口而不是认具体类</b>：GRW-3 摆幅 / AN-2 永世 这类"双结构"能力必须<b>同时</b>给出
/// "手牌上限 +N"与"间隙容量 +M"两个数，若把容量加成写死在 <c>AeonglassGapCapacityPower</c> 里，
/// 它们就得再挂一个纯容量的能力（一张牌两个状态图标）。
/// </para>
/// <para>
/// 口径与 M7 的搁浅通知一致：**能力在场即生效、离场即失效**（读的时候求和，不做加减记账）。
/// </para>
/// </remarks>
internal interface IGapCapacitySource
{
    /// <summary>这个能力给该玩家加多少间隙容量（可以是 0）。</summary>
    int GapCapacityBonus { get; }
}
