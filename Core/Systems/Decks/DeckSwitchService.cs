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
/// ④ ⚠️ 切完必须重建战斗牌堆（<c>DeckPileRebuild.RebuildCombatPilesAsync</c>），否则手牌/牌堆还停在旧副。
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

        DeckSeeding.EnsureInitialized(player);
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

        DeckSeeding.EnsureInitialized(player);
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

        if (!DeckPileRebuild.SwapMasters(player))
        {
            return DeckSwitchOutcome.Fail("搬运卡组母本失败");
        }

        DeckState.SetActiveIndex(player, to);

        var hot = mode == DeckSwitchMode.Manual
                  && AeonglassSettingStore.Current.DecksHotSwitch
                  && CombatManager.Instance.IsInProgress;

        if (hot)
        {
            await DeckPileRebuild.RebuildCombatPilesAsync(player, choiceContext);
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

    /// <summary>切换模式（只影响日志与"要不要重建战斗牌堆"）。</summary>
    private enum DeckSwitchMode
    {
        Auto,
        Manual,
    }
}
