using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 「加入卡组 B」的<b>钩子补齐</b>：让新加入的牌也带上副归属。
/// </summary>
/// <remarks>
/// ① 职责：拦卡牌入组并补记录。
/// ② 调用时机：牌组增删时。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class DeckAddHooks
{
    /// <summary>能不能把这张牌加进牌组；不能就带出是谁挡的。</summary>
    public static bool CanAdd(CardModel card, out AbstractModel? preventer)
    {
        preventer = null;
        try
        {
            return Hook.ShouldAddToDeck(card.Owner.RunState, card, out preventer);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} ShouldAddToDeck 抛异常（按允许处理）：{ex.GetType().Name}: {ex.Message}");
            return true;
        }
    }

    /// <summary>按原版口径改造这张牌（没有相应效果就原样返回）。</summary>
    public static CardModel Modify(CardModel card, out List<AbstractModel> modifyingModels)
    {
        modifyingModels = [];
        try
        {
            return Hook.ModifyCardBeingAddedToDeck(card.Owner.RunState, card, out modifyingModels);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} ModifyCardBeingAddedToDeck 抛异常（按不改造处理）："
                + $"{ex.GetType().Name}: {ex.Message}");
            return card;
        }
    }
}
