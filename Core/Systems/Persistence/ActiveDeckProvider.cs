using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Systems.Persistence;

/// <summary>
/// 「当时的当前卡组」的<b>唯一入口</b>。
/// </summary>
/// <remarks>
/// <para>
/// 搁浅的归队目标、间隙的跨场数据、以及将来的 双卡组（双卡组）都依赖"此刻哪一副牌组是生效的"。
/// <b>双卡组 落地后这件事仍然是 <c>player.Deck</c></b> —— 因为 双卡组 用的是"**内容互换**"语义：
/// "生效中的那一副"始终真实地躺在 <c>player.Deck</c> 里，另一副存在 <c>deck_b</c>
/// （<c>RunPersistent</c> 牌堆）里。好处是所有"把牌放进当前卡组"的老调用点**一行都不用改**。
/// </para>
/// <para>
/// 纪律：任何"要把牌放进当前卡组"的地方都调 <see cref="DeckOf" />，<b>不要</b>直接写 <c>player.Deck</c>。
/// </para>
/// </remarks>
internal static class ActiveDeckProvider
{
    /// <summary>此刻生效的卡组（双卡组 之后仍是 <c>player.Deck</c>，见类型注释）。</summary>
    public static CardPile DeckOf(Player player) => player.Deck;

    /// <summary>当前卡组里有多少张（日志 / UI 用）。</summary>
    public static int CountOf(Player player) => DeckOf(player).Cards.Count;

    /// <summary>
    /// 当前生效的是哪一副（0 = A，1 = B）。双卡组 关掉时恒 0。
    /// </summary>
    public static int ActiveIndexOf(Player player) => DeckPiles.ActiveIndexOf(player);

    /// <summary>
    /// 给 UI / 日志用的一句话：<b>当前</b>生效的是哪一副、有几张。
    /// </summary>
    /// <remarks>搁浅 归队时打的就是这个（"回到当时的当前卡组"）。</remarks>
    public static string DescribeActiveDeck(Player player) =>
        DeckModule.Enabled ? DeckPiles.DescribeActive(player) : $"当前牌组（{CountOf(player)} 张）";

    /// <summary>
    /// 给 UI 用的一句话：<b>下一场战斗</b>会用哪一副（自动轮换开着时 = 另一副）。
    /// </summary>
    public static string DescribeNextCombatDeck(Player player) =>
        DeckModule.Enabled ? DeckPiles.DescribeNext(player) : DescribeActiveDeck(player);
}
