using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 牌堆的<b>快照与静默搬运</b>（RemoveInternal/AddInternal，不发事件、不触发钩子）。
/// </summary>
/// <remarks>
/// ① 职责：<c>Capture</c> / <c>Restore</c>（按每张堆自身 <c>Type</c> 存取，乱流对调也安全）。
/// ② 调用时机：回档抓取/恢复。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal readonly record struct PileRestoreResult(int Restored, int Dropped);

/// <summary>
/// 牌堆的快照与静默搬运。
/// 全部走 <c>RemoveInternal/AddInternal(silent: true)</c>：不发 UI 事件、不触发 hook、不抽牌不洗牌。
/// </summary>
internal static class PileOps
{
    internal static Dictionary<PileType, List<CardModel>> Capture(Player player)
    {
        var result = new Dictionary<PileType, List<CardModel>>();
        if (player.PlayerCombatState is not { } combat)
        {
            return result;
        }

        foreach (var pile in combat.AllPiles)
        {
            result[pile.Type] = pile.Cards.ToList();
        }

        return result;
    }

    /// <summary>
    /// 清空所有战斗牌堆，再按快照顺序放回。
    /// 快照后新增的卡（如生成的衍生牌）视为"当时不存在"：脱离战斗状态，不再留在任何牌堆。
    /// </summary>
    internal static PileRestoreResult Restore(Player player, Dictionary<PileType, List<CardModel>> snapshot)
    {
        if (player.PlayerCombatState is not { } combat)
        {
            return default;
        }

        var piles = combat.AllPiles;
        var snapshotCards = snapshot.Values.SelectMany(cards => cards).ToHashSet();
        var liveCards = piles.SelectMany(pile => pile.Cards).ToHashSet();

        foreach (var pile in piles)
        {
            foreach (var card in pile.Cards.ToList())
            {
                pile.RemoveInternal(card, silent: true);
            }
        }

        var restored = 0;
        foreach (var pile in piles)
        {
            if (!snapshot.TryGetValue(pile.Type, out var cards))
            {
                continue;
            }

            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                Reflect.Set(card, Coordinates.CardModel.Owner, player);
                card.HasBeenRemovedFromState = false;
                pile.AddInternal(card, i, silent: true);
                restored++;
            }

            // 手动补一次通知：让联网卡牌表与跟踪器知道内容变了（silent 搬运不会发）。
            pile.InvokeContentsChanged();
        }

        var dropped = 0;
        foreach (var card in liveCards)
        {
            if (snapshotCards.Contains(card))
            {
                continue;
            }

            card.HasBeenRemovedFromState = true;
            Reflect.Set(card, Coordinates.CardModel.Owner, null);
            dropped++;
        }

        return new PileRestoreResult(restored, dropped);
    }
}
