using HarmonyLib;

using Godot;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Presentation.Combat;
using STS2_Aeonglass.Core.Systems;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Presentation.Support;

using STS2_Aeonglass.Core.Systems.Gap;
namespace STS2_Aeonglass.Core.Presentation.Combat.Gap;
/// <summary>回归（唤回）那半条流水线的<b>分册</b>（<c>AeonglassGapSequence</c> 的 partial 部分）。</summary>
/// <remarks>
/// ① 职责：间隙卡淡出 → 左传送门 → 粒子成形 → 落回手牌。
/// ② 调用时机：<c>RecallAsync</c> 内部。
/// ③ 依赖：同主文件（成员共享，可直调）。
/// ④ ⚠️ 与主文件同属一个类，改私有字段要一起看；位置调参用文件顶部的常量。
/// </remarks>

internal static partial class AeonglassGapSequence
{

    /// <summary>
    /// 「回归」的完整入口（机制 + 表现）：**必然失败时什么都不演**，直接把机制的失败原因带回去。
    /// </summary>
    /// <remarks>
    /// <para>顺序照用户 2026-10-12 的口径：</para>
    /// <list type="number">
    ///   <item><description>间隙里的卡面**透明化消失**（那几张是显示层的镜像，摘出来由我们摆布）。</description></item>
    ///   <item><description>左边那道门**开启**（和 ① 同时开始）。</description></item>
    ///   <item><description>从每张卡的位置撒一把**碎片粒子**飞向画面中央。</description></item>
    ///   <item><description>粒子在中央**聚成卡牌**（用 <c>NCard.Create</c> 造一张真卡节点）。</description></item>
    ///   <item><description>机制层跑 <see cref="GapRecallActions.Recall" />：**丢弃手牌**（走本体表现）。</description></item>
    ///   <item><description>中央那张卡**从上方落进手牌**，到位后交给手牌容器（容器自己会把它摆进槽位）。</description></item>
    /// </list>
    /// <para>
    /// 第 ⑥ 步是这里唯一"取巧"的地方：机制端用 `deferHandVisual` **不生成卡面**（否则会和我们的卡面重影），
    /// 由我们造的那张卡节点接进手牌容器 —— 这与本体 <c>GiveToAnotherPlayer</c> 的做法同源。
    /// </para>
    /// </remarks>
    internal static async Task<GapResult> RecallAsync(Player player)
    {
        var plan = GapRecallPlan.PlanRecall(player);
        if (plan.Count == 0 || OperationBarrier.IsBusy)
        {
            return await GapRecallActions.Recall(player);
        }

        if (NCombatRoom.Instance?.Ui is not { } ui
            || AeonglassGapPortals.Instance is not { } portals
            || !portals.TryGetPortalCenter(!RecallFromLeft, out var portalCenter))
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 传送门不可用 ⇒ 回归退回本体表现");
            return await GapRecallActions.Recall(player);
        }

        using var barrier = OperationBarrier.Enter("间隙换回");
        try
        {
            // ③④⑤ 的落点：手牌上方那条线（卡面 / 粒子 / 卡牌都在战斗 UI 这一层里飘，用全局坐标最省事）
            var formBase = FormBasePosition(ui);

            // ① 把间隙显示层的卡面摘出来（之后由我们演；显示牌堆那些克隆稍后由镜像同步自己撤）
            var clones = StageGapViewCards(ui, player);

            // ② 开门（和 ① 同时开始）
            // suckIn: false = 漩涡往外吐（唤回：牌从门里出来）
            var openTask = portals.PlayOpenAsync(right: !RecallFromLeft, OpenDuration, suckIn: false);

            // ① 间隙卡面**原地**透明化消失
            await FadeGapCardsAsync(clones);

            await openTask;

            // ③ 粒子（= 本体的飞行特效）从左传送门出现 → 飞到"手牌上方"
            await PlayDoorFlyAsync(ui, player, plan, portalCenter, formBase);

            // ④ 粒子还原成卡牌
            var formed = await FormCardsAsync(ui, plan, formBase);

            // ⑤ 机制层：丢弃手牌（本体表现）+ 把间隙的牌**静默**放进手牌（卡面由我们下面接）
            var result = await GapRecallActions.Recall(player, deferHandVisual: true);
            if (!result.Ok)
            {
                FreeNodes(formed);
                return result;
            }

            // ⑥ 卡牌从上方落进手牌
            await PlayRecallDropAsync(ui, formed);

            CombatUiRefresh.RefreshAll(player);
            return result;
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Gap} 回归动画抛异常（机制已完成，动画被丢弃）："
                + $"{ex.GetType().Name}: {ex.Message}");

            try
            {
                await GapViewPile.SyncAsync(player);
                CombatUiRefresh.RefreshAll(player);
            }
            catch (Exception inner)
            {
                Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Gap} 兜底补同步也失败了：{inner.Message}");
            }

            return GapResult.Success("回归（动画异常，机制已完成）");
        }
        finally
        {
            // 无论成功、失败还是抛异常，门都必须合上（否则会卡在"开着"的状态里）
            await ClosePortalQuietlyAsync();
        }
    }

    /// <summary>④：粒子"还原成卡牌"（每张间隙卡造一张真卡节点，在基准线上从 0 放大淡入）。</summary>
    private static async Task<List<StagedCard>> FormCardsAsync(
        NCombatUi ui, IReadOnlyList<CardModel> plan, Vector2 formBase)
    {
        var formed = new List<StagedCard>();
        if (NCombatRoom.Instance is not { } room)
        {
            return formed;
        }

        var tween = room.CreateTween().SetParallel();

        for (var i = 0; i < plan.Count; i++)
        {
            var node = NCard.Create(plan[i]);
            if (node is null)
            {
                continue;
            }

            var spot = FormSpot(formBase, i, plan.Count);

            node.Name = "GapRecallCard";
            node.Scale = Vector2.Zero;
            node.Modulate = new Color(1f, 1f, 1f, 0f);
            ui.AddChildSafely(node);
            node.GlobalPosition = spot;

            formed.Add(new StagedCard(plan[i], node, spot, Vector2.One));

            var delay = i * Stagger;
            tween.TweenProperty(node, "scale", Vector2.One, RecallFormDuration)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(node, "modulate", Colors.White, RecallFormDuration).SetDelay(delay);
        }

        await tween.AwaitFinished(room);
        return formed;
    }

    /// <summary>"手牌上方"那条基准线：手牌区中心 + <see cref="RecallFormOffset" />。</summary>
    private static Vector2 FormBasePosition(NCombatUi ui) =>
        ui.Hand is { } hand
            ? hand.GlobalPosition + hand.Size * 0.5f + RecallFormOffset
            : ui.GlobalPosition + ui.Size * 0.5f + RecallFormOffset;

    /// <summary>多张牌在基准线上的横向排布（居中分摊，间距 = <see cref="RecallFormSpacing" />）。</summary>
    private static Vector2 FormSpot(Vector2 formBase, int index, int count) =>
        formBase + Vector2.Right * ((index - (count - 1) * 0.5f) * RecallFormSpacing);

    /// <summary>
    /// 把间隙**显示层**（<see cref="NModExtraHand" /> 容器）里的卡面摘出来、按原位置改挂到战斗 UI 根。
    /// </summary>
    /// <remarks>
    /// 不用 <c>NCard.FindOnTable</c>：那是按牌堆类型分派的，我们自己这个牌堆类型它管不着；
    /// 走容器自己的 <c>GetCard</c> 最直接。摘走之后 holder 会因为节点离开场景树而自动把
    /// <c>CardNode</c> 置空（<c>NCardHolder.OnChildExitingTree</c>），不会留悬空引用。
    /// </remarks>
    private static List<StagedCard> StageGapViewCards(NCombatUi ui, Player player)
    {
        var staged = new List<StagedCard>();
        if (NodeSearch.FindDescendant<NModExtraHand>(ui, hand => string.Equals(hand.Definition?.Id, GapViewPile.ZoneId, StringComparison.OrdinalIgnoreCase)) is not { } hand || GapViewPile.Zone(player) is not { } view)
        {
            return staged;
        }

        foreach (var clone in view.Cards.ToList())
        {
            var node = hand.GetCard(clone);
            if (node is null || !NodeLifetime.IsAlive(node))
            {
                continue;
            }

            var start = node.GlobalPosition;
            var startScale = UiGeometry.RelativeScale(node, ui);

            try
            {
                node.GetParent()?.RemoveChildSafely(node);
                ui.AddChildSafely(node);
                node.Scale = startScale;
                node.GlobalPosition = start;
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 摘间隙卡面 {clone.Id.Entry} 失败（这张不演）：{ex.Message}");
                continue;
            }

            staged.Add(new StagedCard(clone, node, start, startScale));
        }

        return staged;
    }

    /// <summary>
    /// ①：间隙里的卡面**原地透明化**消失（只动 <c>modulate</c> 的 alpha —— 不动位置、不动缩放）。
    /// </summary>
    /// <remarks>
    /// 用户 2026-10-12 特别要求"先查能否变透明"：这里把**起始/结束的 alpha 打进日志**，
    /// 一眼就能看出透明化有没有真的生效（对不上就是容器那层在覆盖，告诉我即可）。
    /// </remarks>
    private static async Task FadeGapCardsAsync(List<StagedCard> clones)
    {
        if (clones.Count == 0)
        {
            return;
        }

        if (NCombatRoom.Instance is not { } room)
        {
            FreeNodes(clones);
            return;
        }

        var before = new List<string>();
        var tween = room.CreateTween().SetParallel();

        for (var i = 0; i < clones.Count; i++)
        {
            var item = clones[i];
            if (!NodeLifetime.IsAlive(item.Node))
            {
                continue;
            }

            var modulate = item.Node.Modulate;
            before.Add(modulate.A.ToString("0.00"));
            tween.TweenProperty(item.Node, "modulate", new Color(modulate.R, modulate.G, modulate.B, 0f), RecallFadeDuration)
                .SetDelay(i * Stagger);
        }

        await tween.AwaitFinished(room);

        var after = clones
            .Where(item => NodeLifetime.IsAlive(item.Node))
            .Select(item => item.Node.Modulate.A.ToString("0.00"))
            .ToList();

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Gap} 间隙卡面原地透明化：{clones.Count} 张，"
            + $"alpha 起 [{string.Join(",", before)}] → 止 [{string.Join(",", after)}]");

        FreeNodes(clones);
    }

    /// <summary>⑥：把中央那几张卡从上方"落"进手牌，到位后交给手牌容器。</summary>
    private static async Task PlayRecallDropAsync(NCombatUi ui, List<StagedCard> formed)
    {
        if (NCombatRoom.Instance is not { } room || ui.Hand is not { } hand || formed.Count == 0)
        {
            FreeNodes(formed);
            return;
        }

        // 落点：手牌区中心（真正的槽位由手牌容器接管之后再摆）
        var target = hand.GlobalPosition + hand.Size * 0.5f + RecallDropOffset;

        var tween = room.CreateTween().SetParallel();
        for (var i = 0; i < formed.Count; i++)
        {
            var item = formed[i];
            if (!NodeLifetime.IsAlive(item.Node))
            {
                continue;
            }

            tween.TweenProperty(item.Node, "global_position", target, RecallDropDuration)
                .SetDelay(i * Stagger)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.In);
        }

        await tween.AwaitFinished(room);

        foreach (var item in formed)
        {
            if (!NodeLifetime.IsAlive(item.Node))
            {
                continue;
            }

            // 没进手牌（手牌满 / 半路被移走）⇒ 卡面直接丢掉，绝不让它挂在半空
            if (item.Card.Pile?.Type != PileType.Hand)
            {
                item.Node.QueueFreeSafely();
                continue;
            }

            try
            {
                item.Node.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);

                // ⚠️ 我们是"手搓"进手牌的（机制端特意 skipVisuals：否则会和上面那张飞下来的卡面重影），
                // 所以本体"有卡进手牌"的那一连串反应不会自己跑 —— 其中最要紧的是"能打出"的高亮。
                // `NPlayerHand.Add` 回来的 holder 手动刷一次 `UpdateCard()`：它才会去算
                // `Model.CanPlay()` 并 AnimShow 高亮（见 NHandCardHolder.UpdateCard）。
                hand.Add(item.Node).UpdateCard();
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} {item.Card.Id.Entry} 接进手牌失败（改为丢弃卡面）：{ex.Message}");
                item.Node.QueueFreeSafely();
            }
        }
    }

    /// <summary>把两道门都合上（拿不到门 / 出错都只记一笔，绝不再抛）。</summary>
    private static async Task ClosePortalQuietlyAsync()
    {
        try
        {
            if (AeonglassGapPortals.Instance is { } portals)
            {
                await portals.PlayCloseAsync(right: false, CloseDuration, suckIn: false);
                await portals.PlayCloseAsync(right: true, CloseDuration, suckIn: false);
            }
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Gap} 关门失败（已忽略）：{ex.Message}");
        }
    }
}
