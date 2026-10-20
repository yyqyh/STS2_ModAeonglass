using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Stasis;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>统计"所有卡组中的搁浅牌"（岸击用）：生效副 + 非生效副 + 搁浅区。</summary>
/// <remarks>
/// ① 只认实现了 <see cref="IStasisCard" /> 的牌（关键词「搁浅」的机制载体）。
/// ② 不统计战斗内临时生成的副本 —— 它们不在"卡组"里。
/// ③ 读 <c>DeckPiles.InactivePileOf</c> 的同一入口，双卡组关闭时自然退化成单副。
/// </remarks>
internal static class StasisCount
{
    public static int CountAll(Player player)
    {
        var count = player.Deck.Cards.Count(card => card is IStasisCard);

        if (DeckPiles.InactivePileOf(player) is { } inactive)
        {
            count += inactive.Cards.Count(card => card is IStasisCard);
        }

        count += StasisStore.ZoneCards(player).Count(card => card is IStasisCard);
        return count;
    }
}
