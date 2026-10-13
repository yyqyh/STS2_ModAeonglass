using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;

namespace STS2_Aeonglass.Core.Systems.Zones;

/// <summary>
/// M2 的**显示层镜像**：把「间隙」存储牌堆（Headless，负责序列化持久化）镜像到一个
/// <c>ExtraHand</c> 显示牌堆（战斗内临时），卡面靠**原版可视流程**挂上/撤下。
/// </summary>
/// <remarks>
/// <para>
/// 用户 2026-10-12 定的结构：**存储与显示分离** ——
/// </para>
/// <list type="number">
///   <item><description><b>存储牌堆</b>（<c>time_gap</c>，`RunPersistent` + **Headless**）：真相所在，随存档序列化；不显示。</description></item>
///   <item><description><b>显示牌堆</b>（<c>time_gap_view</c>，`CombatOnly` + **ExtraHand**）：只是"投影"，每场战斗重建。</description></item>
/// </list>
/// <para>
/// 为什么必须这么做：RitsuLib 的额外手牌**不订阅牌堆事件**，卡面是靠原版可视流程挂上去的
/// （见其 <c>ModExtraHandCard*Patch</c>）⇒ 镜像必须走 <c>CardPileCmd</c> 才看得见。
/// 链接关系用 <c>clone.DeckVersion = 存储卡</c>（本体"战斗克隆指向母本"的既有约定）。
/// </para>
/// </remarks>
internal static class GapView
{
    private static PileType? _viewPileType;
    private static string _viewId = "-";

    /// <summary>显示牌堆是否已注册。</summary>
    public static bool IsReady => _viewPileType is not null;

    /// <summary>显示牌堆的完整注册 id（给"按 id 找额外手牌容器"用）。</summary>
    public static string ZoneId => _viewId;

    /// <summary>由 <see cref="ZonesModule.RegisterTimeGap" /> 在注册显示牌堆后调用。</summary>
    public static void Configure(ModCardPileDefinition definition)
    {
        _viewPileType = definition.PileType;
        _viewId = definition.Id;
    }

    /// <summary>显示牌堆（没注册 / 玩家为空时为 null）。</summary>
    public static CardPile? Zone(Player? player) =>
        player is not null && _viewPileType is { } type ? CardPile.Get(type, player) : null;

    /// <summary>显示牌堆里的克隆（按 <c>DeckVersion</c> 反查）。</summary>
    private static CardModel? CloneOf(CardPile view, CardModel stored) =>
        view.Cards.FirstOrDefault(c => ReferenceEquals(c.DeckVersion, stored));

    /// <summary>
    /// 把显示牌堆同步成"存储牌堆的投影"（幂等；每次内容变化后调一次）。
    /// </summary>
    /// <remarks>
    /// 增：缺的克隆 → <c>combat.CloneCard</c> + <c>CardPileCmd.Add</c>（走原版可视流程，卡面当场挂上）。
    /// 删：孤立的克隆 → <c>CardPileCmd.RemoveFromCombat</c>（显示堆是 <c>CombatOnly</c>，
    /// RitsuLib 的补丁让它算战斗牌堆 ⇒ 这条路可用，卡面当场撤掉）。
    /// </remarks>
    public static async Task SyncAsync(Player? player)
    {
        if (player is null || !IsReady || !TimeGap.IsReady)
        {
            return;
        }

        var view = Zone(player);
        var stored = TimeGap.Zone(player);
        if (view is null || stored is null)
        {
            return;
        }

        if (player.Creature.CombatState is not CombatState combat)
        {
            return;
        }

        // ① 补缺：存储里有、显示里没有的 → 克隆一份挂进显示牌堆（走 CardPileCmd ⇒ 有卡面）。
        foreach (var card in stored.Cards.ToList())
        {
            if (CloneOf(view, card) is not null)
            {
                continue;
            }

            try
            {
                var clone = combat.CloneCard(card);
                clone.DeckVersion = card; // 链接：这份克隆是"哪一张存储卡的投影"
                var result = await CardPileCmd.Add(
                    clone, view, CardPilePosition.Bottom, null, skipVisuals: false);

                if (!result.success)
                {
                    Const.Logger.Warn($"[{Const.ModId}] [M2] 镜像 {card.Id.Entry} 失败（success=false）");
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] [M2] 镜像 {card.Id.Entry} 抛异常：{ex.GetType().Name}: {ex.Message}");
            }
        }

        // ② 清理：显示里残留、存储里已经没有的克隆 → 撤掉（走 CardPileCmd ⇒ 卡面当场消失）。
        var storedSet = stored.Cards.ToHashSet();
        foreach (var clone in view.Cards.ToList())
        {
            if (clone.DeckVersion is { } master && storedSet.Contains(master))
            {
                continue;
            }

            try
            {
                await CardPileCmd.RemoveFromCombat(clone);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] [M2] 撤下镜像 {clone.Id.Entry} 抛异常：{ex.GetType().Name}: {ex.Message}");
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] [M2] 间隙镜像已同步：存储 {stored.Cards.Count} 张 → 显示 {view.Cards.Count} 张");
    }
}
