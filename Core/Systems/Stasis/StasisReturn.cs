using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Persistence;


namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>
/// 搁浅的<b>进出</b>：把牌交给搁浅区、归队回牌组、战斗结束递减。
/// </summary>
/// <remarks>
/// ① 职责：<c>SendToZone</c> / <c>ReturnToDeck</c> / <c>TickAfterCombatEnd</c> + 监听者通知。
/// ② 调用时机：卡牌打出（<c>IStasisCard</c>）、战斗结束顺序链、归队事件。
/// ③ 依赖：<c>StasisStore</c>（存储牌堆）+ <c>DeckPiles</c>（回哪一副）+ 战斗结束顺序链。
/// ④ ⚠️ 递减/归队按"战斗结束"计数；归队的母本要回"当时的当前卡组"。
/// </remarks>
internal static class StasisReturn
{

    /// <summary>
    /// 把牌送进搁浅：先从牌组摘掉母本，再落到 run 级搁浅牌堆。
    /// </summary>
    /// <param name="x">规格里的 X（几场战斗后归队）。<b>打出当场不计</b>，所以内部按 <c>x + 1</c> 存。</param>
    public static async Task<bool> SendToZone(Player player, CardModel master, int x)
    {
        var zone = StasisStore.Zone(player);
        if (zone is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [搁浅] 搁浅牌堆不可用，取消滞留：{master.Id.Entry}");
            return false;
        }

        // 守卫 ①：同一张母本绝不允许在搁浅出现两次（否则倒计时会重复走、归队会重复放）。
        // 由搁浅自己守 —— 复制器（时空裂隙）不需要知道别的机制。
        if (zone.Cards.Contains(master))
        {
            Const.Logger.Info(
                $"[{Const.ModId}] [搁浅] {master.Id.Entry} 已在搁浅，拒绝重复入堆（唯一性）");
            return false;
        }

        // 守卫 ②（Step 3 新增）：搁浅同场滞留上限（定稿 4 张）。
        // 拒绝而不是静默丢弃 —— 玩家看到的应该是"这次滞留没生效"，而不是牌凭空消失。
        var cap = Math.Clamp(AeonglassSettingStore.Current.StasisMaxStasis, 1, 12);
        if (zone.Cards.Count >= cap)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] [搁浅] 搁浅已满（{zone.Cards.Count}/{cap}），拒绝 {master.Id.Entry} 入堆");
            return false;
        }

        if (master.Pile?.Type == PileType.Deck)
        {
            await CardPileCmd.RemoveFromDeck(master, showPreview: false);
        }

        if (master is IStasisCard stasis)
        {
            stasis.CombatsLeft = x + 1;
        }

        if (master.Pile is null)
        {
            // 静默入堆：CardPileCmd.Add 在 HasBeenRemovedFromState 为真时会静默失败。
            zone.AddInternal(master, -1, true);
        }

        // 滞留让母本离队（牌组 −1）⇒ 立刻刷新牌组数量与相关 UI（用户 2026-10-12）。
        Core.Presentation.Combat.CombatUiRefresh.RefreshAll(player);

        Const.Logger.Info(
            $"[{Const.ModId}] [搁浅] 滞留 {master.Id.Entry} → 搁浅"
            + $"（X={x}，存 {x + 1}，所在牌堆={StasisStore.PileName(master.Pile?.Type)}）");

        // 搁浅通知（STA 通道的收益线读它；例：压舱 = 每搁浅一张 +6 挡）。
        await NotifyStrandListeners(player, master, x + 1);

        return true;
    }

    /// <summary>把"有牌被搁浅了"转发给该玩家身上实现了 <see cref="IStasisReturnListener" /> 的能力。</summary>
    /// <remarks>单个监听者抛异常只记日志，不影响搁浅本身。</remarks>
    internal static async Task NotifyStrandListeners(Player player, CardModel master, int combatsLeft)
    {
        foreach (var listener in player.Creature.Powers.OfType<IStasisReturnListener>().ToList())
        {
            try
            {
                await listener.OnCardStranded(player, master, combatsLeft);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] [搁浅] 搁浅通知失败（{listener.GetType().Name}）：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>归队：从搁浅摘下 → 回到<b>当时的当前卡组</b>。</summary>
    public static async Task<bool> ReturnToDeck(Player player, CardModel master)
    {
        var zone = StasisStore.Zone(player);
        if (zone is null || !zone.Cards.Contains(master))
        {
            return false;
        }

        zone.RemoveInternal(master, true);

        // 守卫 ②：它是不是已经在家了？是的话只从搁浅摘掉、不再 Add ——
        // 这一条才是真正的"防凭空多一张"（Add 之前先问一句）。
        var deck = ActiveDeckProvider.DeckOf(player);
        if (deck.Cards.Contains(master))
        {
            Const.Logger.Info(
                $"[{Const.ModId}] [搁浅] {master.Id.Entry} 已在牌组里，只从搁浅摘掉（不重复放回）");
            return true;
        }

        var ok = await PutMasterBackToDeck(player, master);

        // 归队让牌组 +1 ⇒ 立刻刷新牌组数量（用户 2026-10-12：滞留回归后要刷新）。
        Core.Presentation.Combat.CombatUiRefresh.RefreshAll(player);

        Const.Logger.Info(
            $"[{Const.ModId}] [搁浅] 归队 {master.Id.Entry} → {ActiveDeckProvider.DescribeActiveDeck(player)}"
            + $"（成功={ok}）");
        return ok;
    }

    /// <summary>由 run 级订阅者每场战斗结束调用一次：倒计时 −1，到 0 归队。</summary>
    public static async Task TickAfterCombatEnd(IRunState runState)
    {
        var returned = 0;

        foreach (var player in runState.Players)
        {
            var zone = StasisStore.Zone(player);
            if (zone is null || zone.Cards.Count == 0)
            {
                continue;
            }

            foreach (var master in zone.Cards.ToList())
            {
                if (master is not IStasisCard stasis)
                {
                    Const.Logger.Warn($"[{Const.ModId}] [搁浅] {master.Id.Entry} 在搁浅里但不是滞留卡，跳过倒计时。");
                    continue;
                }

                stasis.CombatsLeft -= 1;
                Const.Logger.Info($"[{Const.ModId}] [搁浅] 倒计时 {master.Id.Entry} → {stasis.CombatsLeft}");

                if (stasis.CombatsLeft <= 0)
                {
                    if (await ReturnToDeck(player, master))
                    {
                        returned++;
                    }
                }
            }
        }

        // Step 3：有牌归队 ⇒ 闪一下顶栏「搁浅」按钮（纯表现层，找不到按钮就静默跳过）。
        if (returned > 0)
        {
            Core.Presentation.Combat.AeonglassStasisPileFlash.Flash(returned);
        }
    }

    /// <summary>
    /// 把母本放回当前卡组（<b>唯一的正确入口</b>）。
    /// </summary>
    /// <remarks>
    /// <c>CardPileCmd.RemoveFromDeck</c> 不会把卡从 <c>RunState</c> 里摘掉，只置
    /// <c>HasBeenRemovedFromState = true</c>；而这个标志为真时 <c>CardPileCmd.Add</c> 会静默失败。
    /// 所以：<b>先清标志</b>（若卡已不在 RunState 里，再补一次 <c>AddCard</c>），**再** <c>Add</c>。
    /// </remarks>
    internal static async Task<bool> PutMasterBackToDeck(Player player, CardModel master)
    {
        if (player.RunState is not RunState runState)
        {
            return false;
        }

        master.HasBeenRemovedFromState = false;
        if (!runState.ContainsCard(master))
        {
            runState.AddCard(master, player);
        }

        var result = await CardPileCmd.Add(
            master,
            ActiveDeckProvider.DeckOf(player).Type,
            CardPilePosition.Bottom,
            null,
            skipVisuals: true);
        return result.success;
    }
}
