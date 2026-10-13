using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// M6「加入卡组 B」的原版钩子补齐（执行计划 Step 4 的第 4 条）。
/// </summary>
/// <remarks>
/// 生效那一副的新牌本来就走在原版 <c>PileType.Deck</c> 路径上，钩子天然会响；
/// 缺口只在"直接往**非生效**那副塞牌"这一路 —— 它是我们自己的模组牌堆，本体不认识它。
/// 这里把本体"加进牌组"会跑的两个钩子按同样顺序补上，给将来的卡牌 / 遗物调用
/// （要等 Step 8 有真卡才有东西可对照验证）。
/// <list type="number">
///   <item><description><c>Hook.ShouldAddToDeck</c>：有"禁止加牌"的效果就拒绝，并带出是谁挡的。</description></item>
///   <item><description><c>Hook.ModifyCardBeingAddedToDeck</c>：有"加进来的牌会被改造"的效果就换牌。</description></item>
/// </list>
/// 两个钩子都**不让异常外泄**：出问题就按"允许 / 不改造"处理并打日志，绝不因为一个效果把加牌流程带崩。
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
