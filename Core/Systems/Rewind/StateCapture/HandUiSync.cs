using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 手牌 UI 是"模型 → 节点"的投影：静默改完牌堆后必须重建投影。
/// </summary>
/// <remarks>
/// ① 职责：按模型重建手牌节点（不发事件那套）。
/// ② 调用时机：回档恢复完（<c>RewindUiRefresh</c> 之后）。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class HandUiSync
{
    internal static void Sync(Player player)
    {
        var hand = NPlayerHand.Instance;
        var pile = player.PlayerCombatState?.Hand;
        if (hand is null || pile is null)
        {
            return;
        }

        try
        {
            hand.CancelAllCardPlay();
        }
        catch
        {
            // 取消失败不影响后面的重建
        }

        // 多余 holder：模型已不在手牌
        foreach (var holder in hand.ActiveHolders.ToList())
        {
            var model = holder.CardNode?.Model;
            if (model is null || !pile.Cards.Contains(model))
            {
                hand.RemoveCardHolder(holder);
            }
        }

        // 缺失 holder：手牌里有、界面没有
        for (var i = 0; i < pile.Cards.Count; i++)
        {
            var model = pile.Cards[i];
            if (hand.GetCardHolder(model) is not null)
            {
                continue;
            }

            var node = NCard.Create(model, ModelVisibility.Visible);
            if (node is not null)
            {
                hand.Add(node, i);
            }
        }
    }
}
