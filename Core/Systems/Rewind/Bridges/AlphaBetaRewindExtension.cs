using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Content.Cards.Rare;
using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>
/// 回溯 ↔「α or β」的接缝（<b>order 260</b>）：<b>每次回溯</b>都把这张牌"重新发一张到你手里"，
/// 同时把散在战斗牌堆里的其它实例收回<b>卡组</b> ⇒ 任何时刻都是「卡组一份 + 手上最多一份」。
/// </summary>
/// <remarks>
/// <para>
/// <b>用户 2026-10-15 口径</b>：每次回溯都给；防止回溯等效果叠出多张 ⇒ <b>回溯后没使用的那张回卡组</b>
/// （下一回回溯再发新的）。
/// </para>
/// <para>
/// 落地：<c>OnAfterRestore</c> 里 ① 把抽/弃/打出/消耗区里散落的实例收回卡组（卡组已有就只把实例移出战斗，
/// 不复制）；② 手牌里已经有一张就<b>不再叠</b>；③ 否则新发一张进手牌，卡组那份保留。
/// </para>
/// <para>
/// 只用同步的牌堆 API（<c>RemoveFromCurrentPile / AddInternal / InvokeContentsChanged</c>）：
/// 回溯接缝的三个回调是同步签名，不能 <c>await</c>。
/// </para>
/// </remarks>
internal sealed class AlphaBetaRewindExtension : IRewindExtension
{
    public string Name => "aeonglass-alpha-beta";

    public int Order => 260;

    public void OnCapture(ISnapshotDraft draft)
    {
        // 这张牌的存在与否挂在**卡组**上（本体牌组本来就会被回溯带走）⇒ 不需要额外快照。
    }

    public void OnBeforeRestore(RewindContext ctx)
    {
        // 恢复前不动：等引擎把牌堆/卡组还原完再补发。
    }

    public void OnAfterRestore(RewindContext ctx) => Refill();

    private static void Refill()
    {
        foreach (var player in Players())
        {
            if (player.PlayerCombatState is not { } combat)
            {
                continue;
            }

            var deckHasIt = player.Deck.Cards.Any(card => card is AeonglassAlphaBeta);
            var handHasIt = false;
            var returned = 0;

            foreach (var pile in combat.AllPiles)
            {
                foreach (var card in pile.Cards.ToList())
                {
                    if (card is not AeonglassAlphaBeta)
                    {
                        continue;
                    }

                    if (ReferenceEquals(pile, combat.Hand))
                    {
                        handHasIt = true;
                        continue;
                    }

                    // 散落在别的牌堆里的：收回卡组（卡组已经有一份就只把实例移出战斗，不复制）。
                    card.RemoveFromCurrentPile();
                    returned++;
                    if (!deckHasIt)
                    {
                        player.Deck.AddInternal(card);
                        player.Deck.InvokeContentsChanged();
                        deckHasIt = true;
                    }
                }
            }

            if (!deckHasIt)
            {
                continue; // 本局根本没带这张牌 ⇒ 不凭空发。
            }

            if (handHasIt)
            {
                Const.Logger.Info(
                    $"[{Const.ModId}] α or β：回溯后手上已有一张（未使用的那张留在卡组）⇒ 不再叠第二张");
                continue;
            }

            var copy = player.RunState.CreateCard(ModelDb.Card<AeonglassAlphaBeta>(), player);
            combat.Hand.AddInternal(copy);
            combat.Hand.InvokeContentsChanged();
            Const.Logger.Info(
                $"[{Const.ModId}] α or β：回溯后进入手牌（卡组仍保留一份；收回散落实例 {returned} 张）");
        }
    }

    private static IReadOnlyList<MegaCrit.Sts2.Core.Entities.Players.Player> Players() =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()?.Players ?? [];
}
