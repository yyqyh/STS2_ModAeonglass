using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Content.Cards.Rare;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Systems.AlphaBeta;

/// <summary>
/// <b>α or β</b> 的两条被动（「若未使用回溯」）的唯一读口。
/// </summary>
/// <remarks>
/// <para>
/// 判定 = ① 本局<b>还没用过回溯</b>（<see cref="RunHistory.Rewinds" /> == 0）；② 对应那一副的牌组里带着
/// <see cref="AeonglassAlphaBeta" />：
/// <b>永时副（A）</b> = 给予凋萎时额外给予 1 张；<b>流逝副（B）</b> = 「坍缩」「唤回」只消耗 1 沙砾。
/// </para>
/// <para>
/// ⚠️ 一旦本局用掉第一次回溯，两条被动立刻整体失效（这是"若未使用回溯"的字面口径）。
/// </para>
/// </remarks>
internal static class AlphaBetaPassive
{
    /// <summary>流逝副（B）：坍缩 / 唤回只消耗 1 沙砾。</summary>
    public static bool GapCostReduced(Player player) => Holding(player, DeckSide.B);

    /// <summary>永时副（A）：每次给予凋萎额外 +1 张。</summary>
    public static bool WitherBonus(Player player) => Holding(player, DeckSide.A);

    private static bool Holding(Player player, DeckSide side)
    {
        if (RunHistory.Rewinds(player) > 0)
        {
            return false;
        }

        return player.Deck.Cards.Any(card => card is AeonglassAlphaBeta
            && DeckVariants.SideOf(player, card) == side);
    }
}
