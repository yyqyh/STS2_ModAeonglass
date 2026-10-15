using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;


namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 双卡组的<b>切换服务</b>：主动/自动切换、交换两副的母本、重建战斗牌堆。
/// </summary>
/// <remarks>
/// ① 职责：把"换副"这件事做成一条原子流程（含冷却/额度闸门回话）。
/// ② 调用时机：右键初始遗物（主动）、战斗胜利/结束的 run 钩子（自动）、控制面板（验证）。
/// ③ 依赖：<c>DeckPiles</c>（堆访问）+ <c>DeckState</c>（存档状态）+ <c>DeckSwitchLimit</c>/<c>DeckSwitchCost</c>（闸门与代价）。
/// ④ ⚠️ 切完必须重建战斗牌堆（<c>RebuildCombatPilesAsync</c>），否则手牌/牌堆还停在旧副。
/// </remarks>
internal static class DeckSwitchService
{

    /// <summary>战斗胜利后的<b>自动轮换</b>（不收费；战斗已结束，所以不重建战斗牌堆）。</summary>
    public static async Task<DeckSwitchOutcome> AutoRotateAsync(Player player)
    {
        if (!DeckModule.Enabled || !DeckPiles.IsReady)
        {
            return DeckSwitchOutcome.Fail("双卡组 未启用");
        }

        DeckPiles.EnsureInitialized(player);
        return await SwitchAsync(player, DeckSwitchMode.Auto, null);
    }

    /// <summary>
    /// <b>主动切换</b>（含战斗内热切换）：过闸门 → 付代价 → 换内容 → 战斗中重建牌堆。
    /// </summary>
    public static async Task<DeckSwitchOutcome> ManualSwitchAsync(
        Player player,
        PlayerChoiceContext? choiceContext)
    {
        if (!DeckModule.Enabled)
        {
            return DeckSwitchOutcome.Fail("双卡组 未启用");
        }

        if (!DeckPiles.IsReady)
        {
            return DeckSwitchOutcome.Fail("第二副牌组牌堆没注册（需要重启游戏）");
        }

        var setting = AeonglassSettingStore.Current;
        if (!setting.DecksManualSwitch)
        {
            return DeckSwitchOutcome.Fail("设置里关掉了「主动切换」");
        }

        DeckPiles.EnsureInitialized(player);
        if (DeckPiles.InactivePileOf(player) is null)
        {
            return DeckSwitchOutcome.Fail("第二副牌组牌堆不可用");
        }

        // 闸门 ①：每场战斗主动切换次数上限（0 = 不限，测试期默认）。
        var limit = Math.Clamp(setting.DecksSwitchesPerCombat, 0, 9);
        if (limit > 0 && DeckSwitchLimit.UsedCountThisCombat(player) >= limit)
        {
            return DeckSwitchOutcome.Fail($"本场战斗主动切换已达上限（{limit} 次）");
        }

        // 闸门 ②：主动切换冷却（0 = 无冷却，测试期默认；正式候选"每 3 场 1 次"）。
        var cooldown = Math.Clamp(setting.DecksManualCooldownCombats, 0, 99);
        var since = DeckState.CombatsSinceManualSwitch(player);
        if (!AeonglassSettingStore.DebugUnlimited && cooldown > 0 && since < cooldown)
        {
            return DeckSwitchOutcome.Fail($"主动切换冷却中：还需 {cooldown - since} 场战斗");
        }

        // 闸门 ③：代价（**唯一扣费入口**）。
        // 热切换（战斗中）额外收"沙砾附加费"（Step 4）：把整副牌换掉并重铺这一场比场间换副更值钱。
        var hotNow = setting.DecksHotSwitch && CombatManager.Instance.IsInProgress;
        var extraSand = hotNow ? Math.Clamp(setting.DecksHotSwitchSandCost, 0, 9) : 0;

        if (!DeckSwitchCost.CanPay(player, out var reason, extraSand))
        {
            return DeckSwitchOutcome.Fail(reason);
        }

        if (!await DeckSwitchCost.PayAsync(player, extraSand))
        {
            return DeckSwitchOutcome.Fail("代价没付成功，已取消切换");
        }

        var outcome = await SwitchAsync(player, DeckSwitchMode.Manual, choiceContext);
        if (!outcome.Ok)
        {
            return outcome;
        }

        DeckSwitchLimit.Mark(player);
        DeckState.NoteManualSwitch(player);
        return outcome;
    }

    private static async Task<DeckSwitchOutcome> SwitchAsync(
        Player player,
        DeckSwitchMode mode,
        PlayerChoiceContext? choiceContext)
    {
        var from = DeckPiles.ActiveIndexOf(player);
        var to = 1 - from;

        if (!SwapMasters(player))
        {
            return DeckSwitchOutcome.Fail("搬运卡组母本失败");
        }

        DeckState.SetActiveIndex(player, to);

        var hot = mode == DeckSwitchMode.Manual
                  && AeonglassSettingStore.Current.DecksHotSwitch
                  && CombatManager.Instance.IsInProgress;

        if (hot)
        {
            await RebuildCombatPilesAsync(player, choiceContext);
        }

        // 张数带牌组名，避免"A/B"与"主卡组/牌堆"两种读法混在一起看不清（实测反馈）。
        var text = $"{DeckPiles.NameOf(from)} → {DeckPiles.NameOf(to)}"
                   + $"（{(mode == DeckSwitchMode.Auto ? "自动轮换" : "主动切换")}"
                   + $"{(hot ? " + 战斗内热切换" : string.Empty)}，"
                   + $"现在生效 {DeckPiles.NameOf(to)}={player.Deck.Cards.Count} 张，"
                   + $"另一副 {DeckPiles.NameOf(from)}={DeckPiles.CountOf(player, from)} 张）";

        // 交换会让"牌组"整副换人（数量也变）+ 战斗中热切换还会重铺五堆 ⇒ 立刻全刷 UI（用户 2026-10-12）。
        Core.Presentation.Combat.CombatUiRefresh.RefreshAll(player);

        // 顶栏两颗牌组按钮的图标也要跟着换人（提示词是在悬浮时算的，图标不行）。
        Core.Presentation.Combat.DeckButtonIcons.Refresh(player);

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 切换卡组：{text}");
        return DeckSwitchOutcome.Done(text);
    }

    /// <summary>
    /// 静默搬运母本：把 <c>player.Deck</c> 与"非生效牌堆"的内容**互换**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 全走 <c>CardPile.RemoveInternal</c>/<c>AddInternal</c>（同步、静默）——
    /// 不用 <c>CardPileCmd.RemoveFromDeck</c>：那会为每张牌记地图历史、发移除钩子、播删除动画，
    /// 而"换一副牌"不是"删一堆牌"。也正因为没走那条路，母本的 <c>HasBeenRemovedFromState</c>
    /// 始终是 false，放回时不需要清标志（搁浅 那套标志处理这里用不上）。
    /// </para>
    /// <para>
    /// 两张母本全程都没离开 <c>RunState</c>（只是换了个牌堆），所以**不需要** <c>AddCard/RemoveCard</c>。
    /// </para>
    /// </remarks>
    private static bool SwapMasters(Player player)
    {
        var pile = DeckPiles.InactivePileOf(player);
        if (pile is null)
        {
            return false;
        }

        var deck = player.Deck;
        var deckCards = deck.Cards.ToList();
        var pileCards = pile.Cards.ToList();

        // 先把两边都摘空，再互填 —— 避免"同一步里既读又写"造成张数错位。
        foreach (var card in deckCards)
        {
            if (deck.Cards.Contains(card))
            {
                deck.RemoveInternal(card, true);
            }
        }

        foreach (var card in pileCards)
        {
            if (pile.Cards.Contains(card))
            {
                pile.RemoveInternal(card, true);
            }
        }

        foreach (var card in pileCards)
        {
            if (!deck.Cards.Contains(card))
            {
                deck.AddInternal(card, -1, true);
            }
        }

        foreach (var card in deckCards)
        {
            if (!pile.Cards.Contains(card))
            {
                pile.AddInternal(card, -1, true);
            }
        }

        // 两边的 UI（顶栏牌组按钮、第二副牌堆按键）都要知道内容变了。
        deck.InvokeContentsChanged();
        pile.InvokeContentsChanged();
        return true;
    }

    /// <summary>
    /// 战斗内热切换：把本场战斗的牌堆按新卡组重铺一遍。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 照堕化拉撒路已实机验证过的路径：**只清 <c>PlayerCombatState.AllPiles</c>**
    /// （手牌 / 抽牌 / 弃牌 / 消耗 / 出牌区），<b>不动主卡组</b>；旧手牌是"**移出战斗**"
    /// （<c>CardPileCmd.RemoveFromCombat</c>），<b>不是弃牌、也不是消耗</b> —— 不触发任何弃牌 / 消耗类效果。
    /// </para>
    /// <para>
    /// 重建走本体的 <c>player.PopulateCombatState(rng, combatState)</c>（战斗开始时的同一条流程：
    /// 克隆卡组 → 进抽牌堆 → 洗牌），随后补抽一手（张数 = 换之前的手牌数，没有就按本体的 5 张）。
    /// </para>
    /// <para>
    /// ⚠️ 间隙的间隙牌堆与 双卡组的第二副牌组都是 <c>RunPersistent</c>，**不在** <c>AllPiles</c> 里
    /// ⇒ 热切换不会动它们（"A 组的牌可以躺在 B 的手牌里"这条口径因此天然成立）。
    /// </para>
    /// </remarks>
    private static async Task RebuildCombatPilesAsync(Player player, PlayerChoiceContext? choiceContext)
    {
        if (!CombatManager.Instance.IsInProgress
            || player.PlayerCombatState is not { } combatPiles
            || player.Creature.CombatState is not CombatState combatState)
        {
            return;
        }

        var handBefore = combatPiles.Hand.Cards.Count;

        // ① 本场战斗里现有的牌**全部移出战斗**（不是弃牌 / 消耗）。
        var combatCards = combatPiles.AllPiles.SelectMany(pile => pile.Cards).ToList();
        if (combatCards.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(combatCards, skipVisuals: false);
        }

        // ② 用新卡组重铺（克隆 → 进抽牌堆 → 洗牌）。
        player.PopulateCombatState(player.RunState.Rng.Shuffle, combatState);

        foreach (var pile in combatPiles.AllPiles)
        {
            pile.InvokeContentsChanged();
        }

        // ③ 补抽一手；没有出牌上下文就不抽，等下一次自然抽牌。
        if (choiceContext is null)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 热切换完成（没有出牌上下文，未补抽手牌）");
            return;
        }

        var drawCount = handBefore > 0 ? handBefore : CombatManager.baseHandDrawCount;
        var drawn = (await CardPileCmd.Draw(choiceContext, drawCount, player)).Count();
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 热切换完成，补抽 {drawn}/{drawCount} 张");
    }

    /// <summary>切换模式（只影响日志与"要不要重建战斗牌堆"）。</summary>
    private enum DeckSwitchMode
    {
        Auto,
        Manual,
    }
}
