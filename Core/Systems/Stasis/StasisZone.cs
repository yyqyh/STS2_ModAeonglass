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
/// 搁浅的搁浅：挂在 <c>ModCardPileScope.RunPersistent</c> 牌堆上的场外牌区。
/// </summary>
/// <remarks>
/// <para>
/// <b>三条实测结论（照 Lab A）</b>：
/// </para>
/// <list type="number">
/// <item><description>
/// <b>入堆前必须先解析牌堆</b>：<c>Player.Piles</c> 只含**已创建**的 run 持久牌堆，
/// 而 <c>CardModel.Pile</c> 是"在 owner 的牌堆里找包含我的那个" —— 所以先
/// <c>CardPile.Get(zoneType, player)</c> 把它解析出来，再动牌。
/// </description></item>
/// <item><description>
/// <b>入场必须走静默路径</b>：<c>CardPileCmd.Add</c> 对 <c>HasBeenRemovedFromState == true</c> 的卡
/// <b>静默失败</b>（实测 <c>success = False</c>）⇒ 用 <c>AddInternal</c>。
/// </description></item>
/// <item><description>
/// <b>搁浅里的牌收不到任何 hook</b>（<c>RunState.IterateHookListeners</c> 只遍历 <c>player.Deck.Cards</c>）
/// ⇒ 倒计时只能由 mod 的 run 级订阅者驱动（<see cref="StasisRunHook" />）。
/// </description></item>
/// </list>
/// </remarks>
internal static class StasisZone
{
    private static PileType? _zonePileType;
    private static string _zoneId = string.Empty;

    /// <summary>搁浅牌堆的完整 id（注册后才有值）。</summary>
    public static string ZoneId => _zoneId;

    /// <summary>牌堆是否已经注册好（搁浅 关掉时是 false，卡会退化成普通牌）。</summary>
    public static bool IsReady => _zonePileType is not null;

    /// <summary>由 <see cref="ExtraPileModules.Register" /> 在注册牌堆后调用。</summary>
    public static void Configure(ModCardPileDefinition definition)
    {
        _zonePileType = definition.PileType;
        _zoneId = definition.Id;

        Const.Logger.Info(
            $"[{Const.ModId}] [搁浅] 搁浅牌堆已注册：{definition.Id}"
            + $"（type=0x{(int)definition.PileType:X8}，scope={definition.Scope}）");
    }

    /// <summary>取搁浅牌堆；未注册或未解析时返回 null。</summary>
    public static CardPile? Zone(Player player) =>
        _zonePileType is { } type ? CardPile.Get(type, player) : null;

    /// <summary>搁浅里现在有几张。</summary>
    public static int ZoneCount(Player player) => Zone(player)?.Cards.Count ?? 0;

    /// <summary>搁浅里的牌（快照）。</summary>
    public static IReadOnlyList<CardModel> ZoneCards(Player player) => Zone(player)?.Cards ?? [];

    /// <summary>一行状态（日志 / 调试面板用）。</summary>
    public static string Describe(Player? player)
    {
        if (player is null)
        {
            return "无玩家";
        }

        var zone = Zone(player);
        if (zone is null)
        {
            return "搁浅牌堆不可用";
        }

        if (zone.Cards.Count == 0)
        {
            return "搁浅：空";
        }

        return "搁浅：" + string.Join(", ", zone.Cards.Select(card =>
            card is IStasisCard stasis ? $"{card.Id.Entry}(剩{stasis.CombatsLeft})" : card.Id.Entry));
    }

    /// <summary>
    /// 把牌送进搁浅：先从牌组摘掉母本，再落到 run 级搁浅牌堆。
    /// </summary>
    /// <param name="x">规格里的 X（几场战斗后归队）。<b>打出当场不计</b>，所以内部按 <c>x + 1</c> 存。</param>
    public static async Task<bool> SendToZone(Player player, CardModel master, int x)
    {
        var zone = Zone(player);
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
            + $"（X={x}，存 {x + 1}，所在牌堆={PileName(master.Pile?.Type)}）");

        // 搁浅通知（STA 通道的收益线读它；例：压舱 = 每搁浅一张 +6 挡）。
        await NotifyStrandListeners(player, master, x + 1);

        return true;
    }

    /// <summary>把"有牌被搁浅了"转发给该玩家身上实现了 <see cref="IStasisReturnListener" /> 的能力。</summary>
    /// <remarks>单个监听者抛异常只记日志，不影响搁浅本身。</remarks>
    private static async Task NotifyStrandListeners(Player player, CardModel master, int combatsLeft)
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
        var zone = Zone(player);
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
            var zone = Zone(player);
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
    private static async Task<bool> PutMasterBackToDeck(Player player, CardModel master)
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

    /// <summary>牌堆名：模组牌堆打印注册 id，原版牌堆打印枚举名。</summary>
    private static string PileName(PileType? type) =>
        type is not { } value
            ? "none"
            : ModCardPileRegistry.TryGetId(value, out var id)
                ? id
                : value.ToString();
}
