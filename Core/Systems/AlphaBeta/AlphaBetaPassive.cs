using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Content.Cards.Rare;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Systems.AlphaBeta;

/// <summary>
/// α or β 的两条被动的<b>唯一读口</b>（带着牌 + 本局未用过回溯才生效）。
/// </summary>
/// <remarks>
/// ① 职责：<c>WitherBonus</c>（给予凋萎 +1）/ <c>GapCostReduced</c>（坍缩唤回 1 沙砾）。
/// ② 调用时机：凋萎注入与裂隙代价计算。
/// ⚠️ 一旦本局用掉第一次回溯，两条被动立刻整体失效（这是"若未使用回溯"的字面口径）。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
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
