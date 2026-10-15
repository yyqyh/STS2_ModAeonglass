using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.AlphaBeta;
using STS2_Aeonglass.Core.Systems.Sand;
using STS2_Aeonglass.Core.Systems.Decks;



namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>
/// 坍缩（手牌 → 时空裂隙）的<b>动作</b>：算能搬几张、真正搬、并通知"刚坍缩完"的监听者。
/// </summary>
/// <remarks>
/// ① 职责：<c>PlanCollapse</c>（预测）/ <c>Activate</c>（执行）/ <c>NotifyCollapseListeners</c>（收益触发）。
/// ② 调用时机：坍缩/唤回按键、控制面板、<c>AeonglassGapSequence</c> 的演出流程。
/// ③ 依赖：<c>GapStore</c>（存储与容量）、<c>GapAlternation</c>（交替闸）、沙砾（代价）。
/// ④ ⚠️ 预测与执行必须一致（同一套规则）；代价不足 / 交替不合规时返回失败文案，不半途改状态。
/// </remarks>
internal static class GapCollapseActions
{

    /// <summary>
    /// <b>预测</b>：这一次「坍缩」会搬走手牌里的哪几张（**无任何副作用**，只回答"会飞哪几张"）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 给表现层用（<c>AeonglassGapSequence</c>：它要先知道"演什么"，才能去抓对应的卡节点）。
    /// 返回空 = 这次一定不成功（不在战斗 / 时空裂隙 没注册 / 本场次数用完 / 间隙已满 / 手牌为空）⇒
    /// 表现层<b>什么都不该演</b>，直接走 <see cref="Activate" /> 拿失败原因。
    /// </para>
    /// <para>
    /// <b>预测与执行共用 <see cref="PlanCore" /> 这段规则</b>，所以不会出现"演的和做的不一样"。
    /// </para>
    /// </remarks>
    public static IReadOnlyList<CardModel> PlanCollapse(Player player)
    {
        if (player.PlayerCombatState is not { } piles || GapStore.Zone(player) is null)
        {
            return [];
        }

        // **预测必须与执行端一致**（2026-10-14：必须交替 + 坍缩代价）：任一不满足就"什么都不该演"。
        // ⚠️ 每回合激活次数的上限已取消（用户口径）⇒ 这里不再有"本回合额度"这一条。
        if (!GapAlternation.AllowsAlternation(player, GapAlternation.GapAction.Collapse)
            || GapStore.ActionCostFor(player) > SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId))
        {
            return [];
        }

        // 坍缩现在**先清空间隙**（用户 2026-10-12 口径）⇒ 容量不再是门槛：整手牌都能进去。
        return PlanCore(piles.Hand, GapStore.Capacity);
    }

    /// <summary>规则本体：手牌里**前 <paramref name="room" /> 张**（照手牌顺序，执行端逐字一致）。</summary>
    private static IReadOnlyList<CardModel> PlanCore(CardPile hand, int room) =>
        room <= 0 ? [] : hand.Cards.Take(room).ToList();

    /// <summary>
    /// 复制：<b>找到手牌 → 复制实例进间隙 → 真实移出手牌 → 从抽牌堆补抽等量</b>。
    /// </summary>
    /// <param name="player">做动作的玩家。</param>
    /// <param name="deferMirror">
    /// <c>true</c> = <b>不</b>立刻把间隙镜像挂到显示牌堆上，交给表现层在「卡面进传送门」的那一刻自己调
    /// <see cref="GapViewPile.SyncAsync" />（否则间隙里的卡面会在动画刚开头就冒出来）；默认 <c>false</c> = 老行为。
    /// </param>
    /// <remarks>
    /// <item><description>复制用 <c>CombatState.CloneCard</c>：真实实例、`DeckVersion` 为 null（不污染牌组母本）、保真度高。</description></item>
    /// <item><description>移出手牌用 <b>`skipVisuals: false`</b> + 显式刷新牌堆 —— 否则手牌 UI 会留下"幻影"节点（打不出、点不动）。</description></item>
    /// <item><description>
    /// 表现层会**先把卡节点从手牌摘走**再调这里 ⇒ 本体的移除表现自己就跳过那几张
    /// （`RemoveFromCombat` 找不到卡节点 ⇒ 不放 puff）、也不会留幻影；
    /// 直接调（不走表现层）时仍然是本体那套 puff —— 两条路都不需要分支。
    /// </description></item>
    /// </remarks>
    public static async Task<GapResult> Activate(Player player, bool deferMirror = false)
    {
        if (player.PlayerCombatState is not { } combatPiles || player.Creature.CombatState is not CombatState combat)
        {
            Const.Logger.Info($"[{Const.ModId}] [间隙] 复制被拒：不在战斗中");
            return GapResult.Fail("不在战斗中");
        }

        var gap = GapStore.Zone(player);
        if (gap is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [间隙] 复制被拒：时空裂隙 未启用（间隙牌堆没注册）");
            return GapResult.Fail("时空裂隙 未启用（间隙牌堆没注册）");
        }

        // 空手牌 ⇒ 整次不动（**必须在付代价之前**：否则会"扣了 2 沙砾却什么都没搬"）。
        if (combatPiles.Hand.Cards.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] [间隙] 复制被拒：手牌为空（没有可复制的牌）");
            return GapResult.Fail("手牌为空，没有可复制的牌");
        }

        // —— 前置检查：必须交替 + 沙砾够付（任一不满足则整次不动）——
        //    每回合激活次数的上限已按用户 2026-10-14 口径取消。
        if (!GapAlternation.AllowsAlternation(player, GapAlternation.GapAction.Collapse))
        {
            var blocked = "坍缩被拒（坍缩与回归必须交替：先回归一次）";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} {blocked}");
            return GapResult.Fail(blocked);
        }

        var cost = GapStore.ActionCostFor(player);
        if (cost > 0
            && !await SecondaryResourceCmd.Spend(player, SandResourceRegistry.SandId, cost))
        {
            var have = SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId);
            var blocked = $"坍缩被拒（沙砾不足：需要 {cost}，现有 {have}）";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} {blocked}");
            return GapResult.Fail(blocked);
        }

        // ★ 用户口径（2026-10-12）：「坍缩的时候，要先清理间隙」——
        //   先把存储里旧的副本清掉，再复制手牌。于是间隙里永远是"最近一次坍缩的那一手"，
        //   不会新旧混着，也不再因为装不下而整次失败（容量不再是门槛）。
        //   显示层那几张旧卡面由表现层（AeonglassGapSequence）在动画开头淡出，这里只动存储。
        var cleared = GapStore.ClearStore(gap);
        if (cleared > 0)
        {
            Const.Logger.Info($"[{Const.ModId}] [间隙] 坍缩：先清空间隙（原有 {cleared} 张）");
        }

        var room = GapStore.Capacity;
        var hand = combatPiles.Hand;
        var handBefore = hand.Cards.Count;
        var plan = PlanCore(hand, room);
        // 来源副**先记下来**：下面会把这几张牌移出手牌，先算能保证与"记进间隙的那一份"逐字一致。
        var planSides = plan.Select(card => DeckVariants.SideOf(player, card)).ToList();

        // ① 复制实例 → ② 进间隙（真实实例，牌堆界面可见、可跨战斗）→ ③ 真实移出手牌。
        for (var i = 0; i < plan.Count; i++)
        {
            var card = plan[i];
            var copy = combat.CloneCard(card);

            // ★ 2026-10-14 用户口径：「间隙」卡回到手牌要**保持它原本的差分**。
            //   副本的 DeckVersion 被本体清空了（AfterCloned）⇒ 单看牌堆认不出来源副，
            //   所以在这里把"来源副"记到副本上（随存档走，见 DeckOrigin）。
            DeckOrigin.Stamp(copy, planSides[i]);

            // 存储牌堆是 **Headless** 的 ⇒ 这里静默写就行（显示由 GapViewPile 镜像负责）。
            gap.AddInternal(copy, -1, true);

            // 带着表现移出手牌（走 CardPileCmd 的既有流程；再加一次内容刷新兜底，防"幻影"）。
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: false);
        }

        var copied = plan.Count;
        if (copied == 0)
        {
            return GapResult.Fail("手牌为空，没有可复制的牌");
        }

        // 镜像到显示牌堆（② 的显示层）：走 CardPileCmd ⇒ 额外手牌当场挂上卡面。
        if (!deferMirror)
        {
            await GapViewPile.SyncAsync(player);
        }

        // 兜底再全刷一遍（牌组 / 战斗五堆 / 模组牌堆 / 容器）。
        gap.InvokeContentsChanged();
        Core.Presentation.Combat.CombatUiRefresh.RefreshAll(player);

        // ④ 补抽（**Step 5 起默认取消**）：新口径下坍缩的代价就是"这回合手牌变少"，
        //    不再从抽牌堆补回来。设置里的 mechanism_m2_refill_draw 可以开回旧行为。
        var drawCount = Math.Min(handBefore, copied);
        var drawn = 0;
        if (AeonglassSettingStore.Current.GapRefillDraw)
        {
            drawn = (await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), drawCount, player)).Count();
        }

        GapAlternation.Mark(player, GapAlternation.GapAction.Collapse);

        // 坍缩通知（GRW 通道的收益线读它；例：升级档「深隙」= 坍缩时额外抽 1 张）。
        await NotifyCollapseListeners(player, copied);

        var text = $"坍缩：手牌 {handBefore} 张 → 间隙 +{copied}（{GapStore.CountOf(player)}/{GapStore.Capacity}）"
                   + $" → 移出手牌 {copied}"
                   + (AeonglassSettingStore.Current.GapRefillDraw ? $" → 补抽 {drawn}/{drawCount}" : " → 不补抽");
        var fromB = planSides.Count(side => side == DeckSide.B);
        text += $"｜来源副：A {copied - fromB} / B {fromB}";
        Const.Logger.Info($"[{Const.ModId}] [间隙] {text}");
        return GapResult.Success(text);
    }

    /// <summary>把"刚坍缩完"转发给该玩家身上实现了 <see cref="IGapCollapseListener" /> 的能力。</summary>
    /// <remarks>单个监听者抛异常只记日志，不影响坍缩本身。</remarks>
    private static async Task NotifyCollapseListeners(Player player, int copies)
    {
        foreach (var listener in player.Creature.Powers.OfType<IGapCollapseListener>().ToList())
        {
            try
            {
                await listener.OnCollapsed(player, copies);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 坍缩通知失败（{listener.GetType().Name}）："
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
