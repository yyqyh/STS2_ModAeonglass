using MegaCrit.Sts2.Core.Commands;
using STS2_Aeonglass.Core.Content.Cards.Token;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2_Aeonglass.Core.Systems.AlphaBeta;

using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Systems.Wither;

/// <summary>
/// 凋萎的<b>存储与计数</b>：怪物三堆里各有多少张。
/// </summary>
/// <remarks>
/// ① 职责：WithersInHand/Discard/Deck/Total + DescribePile。
/// ② 调用时机：卡牌读数量、结算读手牌数。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class WitherPool
{
    // ==================== 给"净化"（Step 8 卡表）留的干净接口 ====================

    /// <summary>某只怪现在**手牌里**有几张凋萎（还能被"净化/剥夺"处理的那部分）。</summary>
    public static int WithersInHand(Creature creature) =>
        IntentDeck.TryGet(creature, out var state) ? state.Hand.Count(c => c.IsVoid) : 0;

    /// <summary>某只怪**弃牌堆里**有几张凋萎（还没被抽到的污染）。</summary>
    public static int WithersInDiscard(Creature creature) =>
        IntentDeck.TryGet(creature, out var state) ? state.Discard.Count(c => c.IsVoid) : 0;

    /// <summary>某只怪**抽牌堆里**有几张凋萎（「枯萎」升级档要数它）。</summary>
    public static int WithersInDeck(Creature creature) =>
        IntentDeck.TryGet(creature, out var state) ? state.Deck.Count(c => c.IsVoid) : 0;

    /// <summary>
    /// 这只怪**场上（三个意图堆）**一共有几张凋萎 —— 「净化」（WTH-3）按它算伤害。
    /// </summary>
    /// <remarks>三堆都算：抽牌堆里还没抽到的、手上的、弃牌堆里的。</remarks>
    public static int WithersTotal(Creature creature) =>
        IntentDeck.TryGet(creature, out var state)
            ? state.Hand.Count(c => c.IsVoid) + state.Discard.Count(c => c.IsVoid) + state.Deck.Count(c => c.IsVoid)
            : 0;

    internal static string DescribePile(IReadOnlyList<IntentCard> pile) =>
        pile.Count == 0 ? "(空)" : string.Join(",", pile.Select(c => c.MoveId));
}
