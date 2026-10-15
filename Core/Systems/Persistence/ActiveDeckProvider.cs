using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Systems.Persistence;

/// <summary>
/// 「当时的当前卡组」的<b>唯一入口</b>（跨战斗读取当前生效副）。
/// </summary>
/// <remarks>
/// ① 职责：取当前副的母本牌组。
/// ② 调用时机：搁浅归队、双卡组相关逻辑。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
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
