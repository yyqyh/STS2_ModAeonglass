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

using STS2_Aeonglass.Core.Presentation.UI;
using STS2_Aeonglass.Core.Systems;
using STS2_Aeonglass.Core.Systems.Rewind.Binding;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Presentation.Visuals;

/// <summary>
/// M2 表现层 · 第 2 步 **2-b：坍缩 / 回归的进出动画**（本文件目前只做了<b>坍缩</b>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>坍缩的镜头（用户 2026-10-12 口径）</b>：手牌向上平移 → 传送门开启 → 卡面变化 →
/// 卡牌飞进传送门 → 传送门合上。合上之后卡面才在「间隙」里出现。
/// </para>
/// <para>
/// <b>顺序上有一个关键决定</b>：<b>机制先跑，动画后演</b>。
/// 因为卡节点一旦离开手牌，手牌那套"自己往目标点 lerp"的布局就不会再跟我们抢位置
/// （<c>NHandCardHolder.SetTargetPosition</c> 是个持续动画），而机制里对节点的处理又只有一次机会。
/// 具体步骤：
/// </para>
/// <list type="number">
///   <item><description>用 <see cref="TimeGap.PlanCollapse" /> 预测"会飞哪几张"（无副作用）。</description></item>
///   <item><description>把这几张的**卡节点**从手牌里摘出来、原位置改挂到战斗 UI 根（与本体
///   <c>CardPileCmd.RemoveFromCombat</c> 的做法一致）。</description></item>
///   <item><description>调 <see cref="TimeGap.Activate" /> 做机制层搬家（此时卡节点已不在手牌 ⇒
///   本体的 puff 自动不触发、也不会留幻影），并让它**先别挂间隙镜像**。</description></item>
///   <item><description>演动画；卡面进门的同一刻挂间隙镜像（这就是
///   <c>deferMirror</c> 存在的唯一理由）。</description></item>
/// </list>
/// <para>
/// <b>任何一步拿不到就整段退回本体表现</b>（<see cref="TimeGap.Activate" /> 原样调用），
/// 所以关掉 M2、门没建、抓不到卡节点时，机制照样能用 —— 表现层永远不会把机制弄坏。
/// </para>
/// <para>
/// 改风格只改下面那一块常量；门的"长相"在 <c>.tscn</c> 里（见 <see cref="GapPortals" />）。
/// </para>
/// </remarks>
internal static class GapSequence
{
    // ==================== 可调参数（改这里就行） ====================

    /// <summary>抬起高度（像素，向上）。</summary>
    private const float LiftHeight = 150f;

    /// <summary>抬起用时（秒）。</summary>
    private const float LiftDuration = 0.16f;

    /// <summary>每张牌之间的错开时间（秒）—— 让它们鱼贯而入，而不是糊成一坨。</summary>
    private const float Stagger = 0.045f;

    /// <summary>抬起之后、起飞之前的停顿（秒）—— 用来等门张开。</summary>
    private const float FlyHold = 0.08f;

    /// <summary>开门用时（秒）。</summary>
    private const float OpenDuration = 0.20f;

    /// <summary>关门用时（秒）。</summary>
    private const float CloseDuration = 0.22f;

    /// <summary>坍缩吸进哪一道门（true = 右门）。</summary>
    private const bool FlyRight = true;

    /// <summary>"粒子已经到落点"的判定阈值（像素）。</summary>
    private const float ArriveEpsilon = 2f;

    /// <summary>等粒子到落点的超时（秒）——本体特效最长约 1.5 秒，给足余量；超时也照常往下走。</summary>
    private const float FlyArrivalTimeout = 3f;

    // ===== 回归（用户 2026-10-12 口径：间隙牌透明化消失 → 左门开启 → 粒子移向中央 →
    //       粒子变成卡牌 → 丢弃手牌 → 卡牌从上方进入手牌）=====

    /// <summary>间隙里的卡面"透明化消失"用时（秒）。</summary>
    private const float RecallFadeDuration = 0.26f;

    /// <summary>中央"粒子聚成卡牌"用时（秒）。</summary>
    private const float RecallFormDuration = 0.22f;

    /// <summary>多张牌在中央的横向间距（像素）。</summary>
    private const float RecallFormSpacing = 92f;

    /// <summary>
    /// "下移进入手牌"的目标点：**相对手牌区中心**的偏移（默认 (0,0) = 手牌区中心）。
    /// </summary>
    /// <remarks>
    /// 卡面从 <see cref="RecallFormOffset" /> 那条线飞到这里，再由手牌容器接手、精确落进它自己的槽位。
    /// 想改"从哪儿下来 / 下到哪儿"就调这两个偏移。
    /// </remarks>
    private static readonly Vector2 RecallDropOffset = new(0,440);//Vector2.Zero;

    /// <summary>卡牌从中央落进手牌用时（秒）。</summary>
    private const float RecallDropDuration = 0.34f;

    /// <summary>
    /// 「粒子变成卡牌」那条线：**相对手牌区中心的偏移**（负 = 在手牌上方）。
    /// </summary>
    /// <remarks>
    /// 用户 2026-10-12：「移动到手牌上方（同坍缩上移处）」——坍缩的抬起是各张牌在**自己原位**上抬
    /// <see cref="LiftHeight" />，回归没有"原位"可言，所以这里按手牌区中心给一条基准线。
    /// 觉得偏上/偏下/偏左偏右就改这个；多张牌之间的横向间距看 <see cref="RecallFormSpacing" />。
    /// </remarks>
    private static readonly Vector2 RecallFormOffset = new(0f, 300f);

    /// <summary>回归从哪一道门出来（true = 左门）。</summary>
    private const bool RecallFromLeft = true;

    // ================================================================

    /// <summary>本体的"卡牌飞行特效"落点字段（<c>NCardFlyVfx._endPos</c>）——只解析一次。</summary>
    private static AccessTools.FieldRef<NCardFlyVfx, Vector2>? _flyEndPos;
    private static bool _flyEndPosResolved;

    /// <summary>一小张牌在演出期间的"旅行状态"。</summary>
    private readonly record struct StagedCard(CardModel Card, NCard Node, Vector2 Start, Vector2 StartScale);

    /// <summary>
    /// 「坍缩」的完整入口（机制 + 表现）：**必然失败时什么都不演**，直接把机制的失败原因带回去。
    /// </summary>
    internal static async Task<GapResult> CollapseAsync(Player player)
    {
        // ① 预测 + 找门：任意一项拿不到就退回本体表现（机制照常能跑）
        var plan = TimeGap.PlanCollapse(player);
        if (plan.Count == 0 || OperationBarrier.IsBusy)
        {
            return await TimeGap.Activate(player);
        }

        if (GapPortals.Instance is not { } portals
            || !portals.TryGetPortalCenter(FlyRight, out var portalCenter))
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 传送门不可用 ⇒ 坍缩退回本体表现");
            return await TimeGap.Activate(player);
        }

        // ② 抓卡节点（**必须在机制改模型之前** —— 模型一离开手牌，手牌立刻重排）
        var staged = StageHandCards(plan);
        if (staged.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 没抓到卡节点 ⇒ 坍缩退回本体表现");
            return await TimeGap.Activate(player);
        }

        using var barrier = OperationBarrier.Enter("M2坍缩");
        try
        {
            // ⓪ 用户口径：坍缩**先清间隙** —— 先把旧的间隙卡面淡出（存储层由机制端清），
            //    和抬起同时演，不额外拖时间。
            var stale = NCombatRoom.Instance?.Ui is { } combatUi
                ? StageGapViewCards(combatUi, player)
                : [];
            if (stale.Count > 0)
            {
                _ = TaskHelper.RunSafely(FadeGapCardsAsync(stale)); // 和抬起并行演，不等它
            }

            // ③ 机制层搬家（镜像先不挂，等卡面真的进门）
            var result = await TimeGap.Activate(player, deferMirror: true);
            if (!result.Ok)
            {
                // 没搬成 ⇒ 把卡节点还回手牌，别让玩家凭空少几张牌
                ReturnToHand(staged);
                CombatUiRefresh.RefreshAll(player);
                return result;
            }

            // ④ 演出（登记进 VFX 屏障：动画期间 M4 回溯会等它 —— 否则会和正在飞的卡节点抢）
            await VfxBarrier.Track("m2Collapse", PlayAnimAsync(player, portals, staged, portalCenter));
            return result;
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Gap} 坍缩动画抛异常（机制已完成，动画被丢弃）："
                + $"{ex.GetType().Name}: {ex.Message}");

            // 兜底：动画可能死在"挂镜像"之前 ⇒ 这里补上，别留下"间隙里没有卡面"这种半截状态。
            try
            {
                await GapView.SyncAsync(player);
                CombatUiRefresh.RefreshAll(player);
            }
            catch (Exception inner)
            {
                Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Gap} 兜底补同步也失败了：{inner.Message}");
            }

            return GapResult.Success("坍缩（动画异常，机制已完成）");
        }
        finally
        {
            // 无论成功、失败还是抛异常，门都必须合上（否则会卡在"开着"的状态里）
            await ClosePortalQuietlyAsync();
        }
    }

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
    ///   <item><description>机制层跑 <see cref="TimeGap.Recall" />：**丢弃手牌**（走本体表现）。</description></item>
    ///   <item><description>中央那张卡**从上方落进手牌**，到位后交给手牌容器（容器自己会把它摆进槽位）。</description></item>
    /// </list>
    /// <para>
    /// 第 ⑥ 步是这里唯一"取巧"的地方：机制端用 `deferHandVisual` **不生成卡面**（否则会和我们的卡面重影），
    /// 由我们造的那张卡节点接进手牌容器 —— 这与本体 <c>GiveToAnotherPlayer</c> 的做法同源。
    /// </para>
    /// </remarks>
    internal static async Task<GapResult> RecallAsync(Player player)
    {
        var plan = TimeGap.PlanRecall(player);
        if (plan.Count == 0 || OperationBarrier.IsBusy)
        {
            return await TimeGap.Recall(player);
        }

        if (NCombatRoom.Instance?.Ui is not { } ui
            || GapPortals.Instance is not { } portals
            || !portals.TryGetPortalCenter(!RecallFromLeft, out var portalCenter))
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 传送门不可用 ⇒ 回归退回本体表现");
            return await TimeGap.Recall(player);
        }

        using var barrier = OperationBarrier.Enter("M2回归");
        try
        {
            // ③④⑤ 的落点：手牌上方那条线（卡面 / 粒子 / 卡牌都在战斗 UI 这一层里飘，用全局坐标最省事）
            var formBase = FormBasePosition(ui);

            // ① 把间隙显示层的卡面摘出来（之后由我们演；显示牌堆那些克隆稍后由镜像同步自己撤）
            var clones = StageGapViewCards(ui, player);

            // ② 开门（和 ① 同时开始）
            var openTask = portals.PlayOpenAsync(right: !RecallFromLeft, OpenDuration);

            // ① 间隙卡面**原地**透明化消失
            await FadeGapCardsAsync(clones);

            await openTask;

            // ③ 粒子（= 本体的飞行特效）从左传送门出现 → 飞到"手牌上方"
            await PlayDoorFlyAsync(ui, player, plan, portalCenter, formBase);

            // ④ 粒子还原成卡牌
            var formed = await FormCardsAsync(ui, plan, formBase);

            // ⑤ 机制层：丢弃手牌（本体表现）+ 把间隙的牌**静默**放进手牌（卡面由我们下面接）
            var result = await TimeGap.Recall(player, deferHandVisual: true);
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
                await GapView.SyncAsync(player);
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

    /// <summary>把两道门都合上（拿不到门 / 出错都只记一笔，绝不再抛）。</summary>
    private static async Task ClosePortalQuietlyAsync()
    {
        try
        {
            if (GapPortals.Instance is { } portals)
            {
                await portals.PlayCloseAsync(right: false, CloseDuration);
                await portals.PlayCloseAsync(right: true, CloseDuration);
            }
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Gap} 关门失败（已忽略）：{ex.Message}");
        }
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
            if (!IsAlive(item.Node))
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
            .Where(item => IsAlive(item.Node))
            .Select(item => item.Node.Modulate.A.ToString("0.00"))
            .ToList();

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Gap} 间隙卡面原地透明化：{clones.Count} 张，"
            + $"alpha 起 [{string.Join(",", before)}] → 止 [{string.Join(",", after)}]");

        FreeNodes(clones);
    }

    /// <summary>
    /// ③：让**本体那套飞行特效**（同坍缩）从左传送门飞到"手牌上方"那条线 —— 就是"粒子"。
    /// </summary>
    /// <remarks>
    /// 做法与坍缩完全一致：先造一张真卡节点放在门心（<c>NCardFlyVfx</c> 的起点取的就是卡节点当前位置），
    /// 再把它的落点改成基准线上该牌的位置。卡面会一路缩小 + 拖尾，正好读作"粒子从门里出来、飞到手上方"。
    /// 它被特效吃掉之后，再由 <see cref="FormCardsAsync" /> 在落点"还原成卡牌"。
    /// </remarks>
    private static async Task PlayDoorFlyAsync(
        NCombatUi ui, Player player, IReadOnlyList<CardModel> plan, Vector2 from, Vector2 formBase)
    {
        var particles = new List<StagedCard>();
        var flights = new List<(Control Node, Vector2 Target)>();
        var fallbackPile = GapView.Zone(player)?.Type ?? PileType.Discard;

        for (var i = 0; i < plan.Count; i++)
        {
            var node = NCard.Create(plan[i]);
            if (node is null)
            {
                continue;
            }

            var spot = FormSpot(formBase, i, plan.Count);
            try
            {
                node.Name = "GapRecallParticle";
                ui.AddChildSafely(node);
                node.GlobalPosition = from;

                var vfx = NCardFlyVfx.Create(
                    node, fallbackPile, isAddingToPile: false, plan[i].Owner.Character.TrailPath);
                if (vfx is null)
                {
                    node.QueueFreeSafely();
                    continue;
                }

                AimFlyAt(vfx, spot);
                ui.AddChildSafely(vfx);
                particles.Add(new StagedCard(plan[i], node, from, Vector2.One));
                flights.Add((node, spot));
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} {plan[i].Id.Entry} 的粒子特效没起来（这颗跳过）："
                    + $"{ex.GetType().Name}: {ex.Message}");
                node.QueueFreeSafely();
            }
        }

        // 只等"飞到手上方"，随后**立刻**收掉粒子、让卡牌开始成形（见 WaitForArrivalAsync 的说明）
        await WaitForArrivalAsync(ui, flights);
        HideNodes(particles);
    }

    /// <summary>"手牌上方"那条基准线：手牌区中心 + <see cref="RecallFormOffset" />。</summary>
    private static Vector2 FormBasePosition(NCombatUi ui) =>
        ui.Hand is { } hand
            ? hand.GlobalPosition + hand.Size * 0.5f + RecallFormOffset
            : ui.GlobalPosition + ui.Size * 0.5f + RecallFormOffset;

    /// <summary>多张牌在基准线上的横向排布（居中分摊，间距 = <see cref="RecallFormSpacing" />）。</summary>
    private static Vector2 FormSpot(Vector2 formBase, int index, int count) =>
        formBase + Vector2.Right * ((index - (count - 1) * 0.5f) * RecallFormSpacing);

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
            if (!IsAlive(item.Node))
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
            if (!IsAlive(item.Node))
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
        if (FindExtraHand(ui, GapView.ZoneId, 0) is not { } hand || GapView.Zone(player) is not { } view)
        {
            return staged;
        }

        foreach (var clone in view.Cards.ToList())
        {
            var node = hand.GetCard(clone);
            if (node is null || !IsAlive(node))
            {
                continue;
            }

            var start = node.GlobalPosition;
            var startScale = RelativeScale(node, ui);

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

    /// <summary>找我们的额外手牌容器（按显示牌堆 id 匹配，和 <c>ExtraHandRefresh</c> 同一套规则）。</summary>
    private static NModExtraHand? FindExtraHand(Node root, string pileId, int depth)
    {
        if (depth > 6)
        {
            return null;
        }

        foreach (var child in root.GetChildren())
        {
            if (child is NModExtraHand hand
                && string.Equals(hand.Definition?.Id, pileId, StringComparison.OrdinalIgnoreCase))
            {
                return hand;
            }

            if (FindExtraHand(child, pileId, depth + 1) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>
    /// 把这几张牌的卡节点从手牌里摘出来、按原位置改挂到战斗 UI 根。
    /// </summary>
    /// <remarks>
    /// `hand.Remove(model)` 会解绑并释放**holder**，而卡节点本身被摘出来（`NCardHolder.Clear` 只摘不销毁）
    /// —— 这正是本体 `CardPileCmd.RemoveFromCombat` 用的那一招。之后这些节点就完全归我们摆布，
    /// 手牌那套持续 lerp 的布局不会再跟我们抢位置。
    /// </remarks>
    private static List<StagedCard> StageHandCards(IReadOnlyList<CardModel> plan)
    {
        var staged = new List<StagedCard>();
        if (NCombatRoom.Instance is not { } room || room.Ui is not { } ui)
        {
            return staged;
        }

        var hand = ui.Hand;
        foreach (var card in plan)
        {
            var node = NCard.FindOnTable(card, PileType.Hand);
            if (node is null)
            {
                continue;
            }

            // 手牌会给 holder 一个扇形缩放（牌多时 <1）—— 摘出来之前先记下**相对战斗 UI** 的那个缩放，
            // 否则换了父节点会突然变大（本体 RemoveFromCombat 也有这个现象，我们顺手修正）。
            // 位置则照本体的做法读 `GlobalPosition` / 写回同一个值，两边必然一致。
            var start = node.GlobalPosition;
            var startScale = RelativeScale(node, ui);

            try
            {
                if (hand is not null && hand.IsAncestorOf(node))
                {
                    hand.Remove(card);
                }
                else
                {
                    node.GetParent()?.RemoveChildSafely(node);
                }

                ui.AddChildSafely(node);
                node.Scale = startScale;
                node.GlobalPosition = start;
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 摘卡节点 {card.Id.Entry} 失败（这张不演）："
                    + $"{ex.GetType().Name}: {ex.Message}");
                continue;
            }

            staged.Add(new StagedCard(card, node, start, startScale));
        }

        return staged;
    }

    /// <summary>把卡节点还回手牌（机制没搬成时的回滚路径）。</summary>
    private static void ReturnToHand(List<StagedCard> staged)
    {
        if (NCombatRoom.Instance?.Ui?.Hand is not { } hand)
        {
            FreeNodes(staged);
            return;
        }

        foreach (var item in staged)
        {
            if (!IsAlive(item.Node))
            {
                continue;
            }

            try
            {
                hand.Add(item.Node);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} {item.Card.Id.Entry} 还回手牌失败（改为直接清掉）：{ex.Message}");
                item.Node.QueueFreeSafely();
            }
        }
    }

    /// <summary>四段演出：抬起 → 开门 → 进门 → 关门（进门的同一刻把间隙镜像挂上）。</summary>
    private static async Task PlayAnimAsync(
        Player player, GapPortals portals, List<StagedCard> staged, Vector2 portalCenter)
    {
        if (NCombatRoom.Instance is not { } room)
        {
            FreeNodes(staged);
            return;
        }

        // ① 抬起："被吸起来"的那一下（错开一点 → 像鱼贯）；**开门和抬起同时开始**，衔接更紧。
        var lift = room.CreateTween().SetParallel();
        var openTask = portals.PlayOpenAsync(FlyRight, OpenDuration);
        for (var i = 0; i < staged.Count; i++)
        {
            var item = staged[i];
            if (!IsAlive(item.Node))
            {
                continue;
            }

            var delay = i * Stagger;
            lift.TweenProperty(item.Node, "global_position", item.Start + Vector2.Up * LiftHeight, LiftDuration)
                .SetDelay(delay)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        }

        await lift.AwaitFinished(room);

        // ② 等门张开到位
        await openTask;
        if (FlyHold > 0f)
        {
            await WaitAsync(room, FlyHold);
        }

        // ③ 变化 + 进门：**交给本体那套"卡牌飞行特效"**（卡面原地缩小 → 带拖尾，就是卡牌加入牌堆时那个资源）
        await PlayVanillaFlyAsync(ui: room.Ui, staged, player, portalCenter);

        // ④ 收尾：清节点 → 卡面在间隙里出现 → 关门
        //    卡面此刻已经"进了门"（同一时刻才被判为到位）⇒ 先藏起来，剩下那点收尾交给本体特效自己做完。
        HideNodes(staged);
        await GapView.SyncAsync(player);
        CombatUiRefresh.RefreshAll(player);

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 坍缩演出结束（飞进门的卡面 {staged.Count} 张）");
    }

    /// <summary>
    /// 用**本体原版资源**把卡面送进门：<see cref="NCardFlyVfx" /> —— 它就是"卡牌加入牌堆"过场里那套
    /// （卡面原地缩小 + 角色拖尾一路跟着 + 沿弧线飞走），连音效都是本体的。
    /// </summary>
    /// <remarks>
    /// 本体这个特效的落点是私有字段 <c>_endPos</c>（工厂方法只接受"牌堆类型 / 生物"），所以这里用
    /// Harmony 的 <c>AccessTools.FieldRefAccess</c> 把它改成门心；**改不到也不报错**——
    /// 那就让它按牌堆落点飞（飞向"间隙"显示区，意思也对）。字段名将来若被本体改掉，只会退化成后者。
    /// </remarks>
    private static async Task PlayVanillaFlyAsync(NCombatUi ui, List<StagedCard> staged, Player player, Vector2 doorCenter)
    {
        var flights = new List<(Control Node, Vector2 Target)>();
        var fallbackPile = GapView.Zone(player)?.Type ?? PileType.Discard;

        for (var i = 0; i < staged.Count; i++)
        {
            var item = staged[i];
            if (!IsAlive(item.Node))
            {
                continue;
            }

            try
            {
                // 拖尾资源用本体那套：角色自己的 card_trail_<角色>.tscn
                var trailPath = item.Card.Owner.Character.TrailPath;
                var vfx = NCardFlyVfx.Create(item.Node, fallbackPile, isAddingToPile: false, trailPath);
                if (vfx is null)
                {
                    // 测试模式之类：本体直接不给特效 ⇒ 卡面别留在半空
                    item.Node.QueueFreeSafely();
                    continue;
                }

                AimFlyAt(vfx, doorCenter);
                ui.AddChildSafely(vfx);
                flights.Add((item.Node, doorCenter));
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} {item.Card.Id.Entry} 的飞行特效没起来（这张直接收掉）："
                    + $"{ex.GetType().Name}: {ex.Message}");
                item.Node.QueueFreeSafely();
            }
        }

        // 只等"飞到门心"，不等本体的 SwooshAwayCompletion（那个还要多等一整段"落点继续缩没"的空档）
        await WaitForArrivalAsync(ui, flights);
    }

    /// <summary>
    /// 等这些卡节点**飞到各自落点**（本体特效把 <c>_card.GlobalPosition</c> 精确设成落点的那一刻）。
    /// </summary>
    /// <remarks>
    /// <b>为什么不等 <c>NCardFlyVfx.SwooshAwayCompletion</c></b>：那个信号是"飞完 + 在落点继续缩到没"
    /// 两段都结束后才发的，中间整整一段什么都看不见的空档。用户 2026-10-12 要求
    /// "粒子特效结束的一瞬间卡牌就要开始出现并从小变大"，所以改以**到达落点**为准 ——
    /// 粒子收掉与卡牌成形就严丝合缝了。
    /// </remarks>
    private static async Task WaitForArrivalAsync(Node owner, List<(Control Node, Vector2 Target)> flights)
    {
        if (flights.Count == 0)
        {
            return;
        }

        var elapsed = 0.0;
        while (elapsed < FlyArrivalTimeout)
        {
            if (owner.GetTree() is not { } tree)
            {
                return;
            }

            await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            if (!GodotObject.IsInstanceValid(owner))
            {
                return;
            }

            elapsed += owner.GetProcessDeltaTime();

            if (flights.All(f => !IsAlive(f.Node) || f.Node.GlobalPosition.DistanceTo(f.Target) <= ArriveEpsilon))
            {
                return;
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Gap} 等粒子落点超时（{FlyArrivalTimeout:0.0}s）——照常继续");
    }

    /// <summary>把本体飞行特效的落点改成 <paramref name="target" />（只解析一次字段；拿不到就按牌堆落点飞）。</summary>
    private static void AimFlyAt(NCardFlyVfx vfx, Vector2 target)
    {
        if (!_flyEndPosResolved)
        {
            _flyEndPosResolved = true;
            try
            {
                _flyEndPos = AccessTools.FieldRefAccess<NCardFlyVfx, Vector2>("_endPos");
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 拿不到 NCardFlyVfx._endPos（本体改过字段名？）"
                    + $"⇒ 飞行特效改为飞向「间隙」显示区：{ex.GetType().Name}");
            }
        }

        if (_flyEndPos is not null)
        {
            _flyEndPos(vfx) = target;
        }
    }

    private static async Task WaitAsync(Node owner, float seconds)
    {
        var tween = owner.CreateTween();
        tween.TweenInterval(seconds);
        await tween.AwaitFinished(owner);
    }

    /// <summary>
    /// 卡节点相对战斗 UI 的累积缩放（= 手牌给 holder 的那个扇形缩放）。
    /// 除以 UI 自己的缩放是为了抵掉窗口拉伸 —— 否则画布缩放会被当成卡面尺寸，卡会明显变大。
    /// </summary>
    private static Vector2 RelativeScale(CanvasItem node, CanvasItem ui)
    {
        var nodeScale = node.GetGlobalTransformWithCanvas().Scale;
        var uiScale = ui.GetGlobalTransformWithCanvas().Scale;
        return new Vector2(
            Mathf.IsZeroApprox(uiScale.X) ? 1f : nodeScale.X / uiScale.X,
            Mathf.IsZeroApprox(uiScale.Y) ? 1f : nodeScale.Y / uiScale.Y);
    }

    /// <summary>
    /// 把这些卡面**藏起来**（不销毁）。它们此刻已经交给本体的飞行特效管了 ——
    /// 特效会在自己结束时连拖尾一起收掉；我们这侧销毁反而会提前掐断它，
    /// 还可能让刚销毁的池实例被下一次 <c>NCard.Create</c> 立刻拿去复用（位置会被旧特效多写一帧）。
    /// </summary>
    private static void HideNodes(List<StagedCard> staged)
    {
        foreach (var item in staged)
        {
            if (IsAlive(item.Node))
            {
                item.Node.Visible = false;
            }
        }
    }

    private static void FreeNodes(List<StagedCard> staged)
    {
        foreach (var item in staged)
        {
            if (IsAlive(item.Node))
            {
                item.Node.QueueFreeSafely();
            }
        }
    }

    private static bool IsAlive(Node? node) => node is not null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();
}
