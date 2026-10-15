namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>搁浅 滞留牌：卡上带一个随存档往返的倒计时。</summary>
/// <remarks>
/// 实现者必须把 <see cref="CombatsLeft" /> 标成 <c>[SavedProperty]</c>，它才会随
/// <c>SerializableCard.Props</c> 入档（否则读档后倒计时归零、牌立刻归队）。
/// </remarks>
internal interface IStasisCard
{
    /// <summary>还剩几次"战斗结束"才归队（内部按 <c>X + 1</c> 存，所以打出当场那次不计）。</summary>
    int CombatsLeft { get; set; }
}
