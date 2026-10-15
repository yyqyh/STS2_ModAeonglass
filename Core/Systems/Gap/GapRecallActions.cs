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
/// 回归（时空裂隙 → 手牌）的<b>动作</b>：算能取回几张、真正取回并丢弃当前手牌。
/// </summary>
/// <remarks>
/// ① 职责：<c>PlanRecall</c>（预测）/ <c>Recall</c>（执行，含缓收/虚无等附加口径）。
/// ② 调用时机：坍缩/唤回按键、控制面板、<c>AeonglassGapSequence</c> 的演出流程。
/// ③ 依赖：<c>GapStore</c>、<c>GapAlternation</c>、<c>GapRecallGuard</c>/<c>GapEtherealGuard</c>/<c>GapCopyRestore</c>。
/// ④ ⚠️ "丢弃当前手牌"是不可逆的一步，失败必须发生在它之前。
/// </remarks>
internal static class GapRecallActions
{


    /// <summary>
    /// 激活：<b>先付代价 → 丢弃当前手牌（进弃牌堆）→ 把间隙里的卡牌实例搬进手牌 → 清空间隙记录</b>。
    /// </summary>
    /// <param name="player">做动作的玩家。</param>
    /// <param name="deferHandVisual">
    /// <c>true</c> = 把间隙的牌放进手牌时**不生成卡面**（<c>skipVisuals</c>），由表现层自己造一张卡节点、
    /// 演完"从上方落进手牌"再接进手牌容器（`NPlayerHand.Add`）。
    /// 用法与本体的 <c>GiveToAnotherPlayer</c> 一致（它也是 skipVisuals 之后自己摆节点）。
    /// 默认 <c>false</c> = 老行为。
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>两道速率闸先过</b>（2026-10-14 定档，见 <see cref="GapAlternation" />）：本回合额度用尽、或与上一次动作没有交替
    /// ⇒ 直接拒绝，**在付代价 / 搬牌之前**。
    /// </para>
    /// <para>代价不足 ⇒ **整次失败、什么都不发生**（不能出现"手牌弃了、副本没回来"的半途状态）。</para>
    /// <para>
    /// 间隙里放的是真实实例 ⇒ 实例被搬走即离开间隙，所以「清除间隙记录」在这里就等于"间隙归空"；
    /// 搬回手牌失败的个别牌会**放回间隙**（宁可留着也不凭空消失），并如实打进日志。
    /// </para>
    /// </remarks>
    public static async Task<GapResult> Recall(Player player, bool deferHandVisual = false)
    {
        if (player.PlayerCombatState is not { } combatPiles || player.Creature.CombatState is not CombatState combat)
        {
            Const.Logger.Info($"[{Const.ModId}] [间隙] 激活被拒：不在战斗中");
            return GapResult.Fail("不在战斗中");
        }

        var gap = GapStore.Zone(player);
        if (gap is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [间隙] 激活被拒：时空裂隙 未启用（间隙牌堆没注册）");
            return GapResult.Fail("时空裂隙 未启用（间隙牌堆没注册）");
        }

        // —— 前置检查：必须交替 ——
        //    ⚠️ 必须在**付代价 / 搬牌之前**：不满足就整次不动（不能出现"手牌弃了、副本没回来"）。
        //    每回合激活次数的上限已按用户 2026-10-14 口径取消。
        if (!GapAlternation.AllowsAlternation(player, GapAlternation.GapAction.Recall))
        {
            var blocked = "回归被拒（坍缩与回归必须交替：先坍缩一次）";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} {blocked}");
            return GapResult.Fail(blocked);
        }

        if (gap.Cards.Count == 0)
        {
            // 这条以前是"静默失败"（不打日志），实测时最难判断 —— 现在必须留痕。
            Const.Logger.Info($"[{Const.ModId}] [间隙] 激活被拒：间隙是空的（{GapStore.Describe(player)}）");
            return GapResult.Fail("间隙是空的");
        }

        var copies = gap.Cards.ToList();
        Const.Logger.Info($"[{Const.ModId}] [间隙] 激活开始：{GapStore.Describe(player)}");

        // ① 先付代价：不足就整次不动（与坍缩同一笔：统一 2 沙砾）。
        var cost = GapStore.ActionCost;
        if (cost > 0 && !await SecondaryResourceCmd.Spend(player, SandResourceRegistry.SandId, cost))
        {
            var have = SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId);
            var blocked = $"激活被拒（沙砾不足：需要 {cost}，现有 {have}）";
            Const.Logger.Info($"[{Const.ModId}] [间隙] {blocked}");
            return GapResult.Fail(blocked);
        }

        // ② 丢弃当前手牌（进弃牌堆）—— 除非本回合挂了 GAP-2「缓收」且间隙够多。
        var hand = combatPiles.Hand;
        var discarded = 0;
        var (keepHand, drawAfterRecall) = GapRecallGuard.Evaluate(player, copies.Count);
        if (keepHand)
        {
            Const.Logger.Info($"[{Const.ModId}] [间隙] 缓收生效：间隙 {copies.Count} 张 ⇒ 本次回归不丢手牌");
        }

        foreach (var card in keepHand ? [] : hand.Cards.ToList())
        {
            var result = await CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Bottom, null, skipVisuals: false);
            if (result.success)
            {
                discarded++;
            }
        }

        Core.Presentation.Combat.CombatUiRefresh.RefreshAll(player);

        // ③ 间隙里的实例 → 手牌（真实实例，搬过去就能打）。
        var moved = 0;
        foreach (var card in copies)
        {
            // 先从**存储**牌堆摘下（静默：存储是 Headless，没有可视流程要照顾）；
            // 显示层那份镜像稍后由 GapViewPile.SyncAsync 统一撤掉（走 CardPileCmd ⇒ 卡面当场消失）。
            if (gap.Cards.Contains(card))
            {
                gap.RemoveInternal(card, true);
            }

            try
            {
                // 跨战斗带过来的实例可能已不在当前战斗状态里，要重新登记进本次战斗 ——
                // 否则搬进手牌也打不出（"幻影"的另一种成因）。
                //
                // ⚠️ 这里**不能**直接 `combat.AddCard(card, player)`：跨战斗/跨存档的实例 Owner 还挂着，
                // 而 `CardModel.Owner` 的 setter 在"已有 owner 且新值非空"时会直接抛
                // `Card X already has an owner.`（2026-10-11 实测 log 抓到的就是这条，栈顶在
                // `CombatState.AddCard` → `CardModel.set_Owner`）。
                // `CombatState.RemoveCard` 的语义正好是"摘牌 + 清 owner"，所以先清干净再登记。
                if (!combat.ContainsCard(card))
                {
                    combat.RemoveCard(card);
                    combat.AddCard(card, player);
                }

                // 跨存档回来时可能丢次级资源费用（本体现在是实例复制，正常路径不会丢），补一道保险。
                GapCopyRestore.ReapplyCosts(card);

                // 虚无（GapStore.Ethereal）：2026-10-14 定档**副本带虚无**（默认 on；设置页 · 时空裂隙 仍可关）。
                // GAP-3「存真」：本回合挂上开关时跳过这一步（副本不带虚无）。
                if (GapStore.Ethereal && !GapEtherealGuard.Active(player))
                {
                    card.AddKeyword(CardKeyword.Ethereal);
                }

                var result = await CardPileCmd.Add(
                    card, PileType.Hand, CardPilePosition.Bottom, null, skipVisuals: deferHandVisual);

                if (result.success)
                {
                    moved++;
                    continue;
                }

                Const.Logger.Warn($"[{Const.ModId}] [间隙] {card.Id.Entry} 搬回手牌失败（success=false）→ 放回间隙");
            }
            catch (Exception ex)
            {
                Const.Logger.Error(
                    $"[{Const.ModId}] [间隙] {card.Id.Entry} 搬回手牌抛异常（已放回间隙）："
                    + $"{ex.GetType().Name}: {ex.Message}");
            }

            // 兜底：搬不进手牌（手牌满 / 卡已被移出状态 / 主人已死 / 抛异常）就放回间隙，别让它凭空消失；
            // 同时保证它还挂在本次战斗上，免得留下"半个实例"。
            if (!gap.Cards.Contains(card))
            {
                gap.AddInternal(card, -1, true);
            }

            try
            {
                if (!combat.ContainsCard(card))
                {
                    combat.AddCard(card, player);
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn($"[{Const.ModId}] [间隙] {card.Id.Entry} 回填战斗登记失败：{ex.Message}");
            }
        }

        Core.Presentation.Combat.CombatUiRefresh.RefreshAll(player);

        // 缓收的升级档：回归完再抽几张。
        if (keepHand && drawAfterRecall > 0)
        {
            var drawn = (await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), drawAfterRecall, player)).Count();
            Const.Logger.Info($"[{Const.ModId}] [间隙] 缓收：回归后补抽 {drawn}/{drawAfterRecall}");
        }

        // 存储搬空了 ⇒ 同步显示层（撤掉那些克隆）。
        await GapViewPile.SyncAsync(player);
        gap.InvokeContentsChanged();
        Core.Presentation.Combat.CombatUiRefresh.RefreshAll(player);

        // ④ 清除"间隙"记录（用户 2026-10-11 的口径）：激活之后间隙必须归空 ——
        //    副本是真实实例，被搬走即离开间隙，所以"记录"这里就是牌堆内容本身。
        //    正常路径到这里已经空了；若还有残留（搬回失败的那几张），如实打出来，不假装成功。
        var leftover = gap.Cards.Count;
        if (leftover > 0)
        {
            Const.Logger.Warn($"[{Const.ModId}] [间隙] 激活后间隙仍有 {leftover} 张（搬回手牌失败，已留在间隙里）");
        }

        // 闸门记账：走到这里 = 代价已付、牌也搬过 ⇒ **这一次激活确实发生了**（哪怕个别牌没搬回），
        // 所以本回合额度占用与"上次动作 = 回归"都按事实记下。
        GapAlternation.Mark(player, GapAlternation.GapAction.Recall);

        var text = $"激活：花 {cost} 沙砾，弃掉手牌 {discarded} 张，间隙 {copies.Count} 张 → 手牌 {moved} 张"
                   + $"，间隙清空（剩 {leftover}）";
        Const.Logger.Info($"[{Const.ModId}] [间隙] {text} ｜ 现在 {GapStore.Describe(player)}");
        return new GapResult(moved > 0, text);
    }
}
