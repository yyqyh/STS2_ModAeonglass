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
/// <summary>动画流水线的<b>共用 helper 分册</b>（等待 / 飞行 / 落点 / 节点释放）。</summary>
/// <remarks>
/// ① 职责：坍缩与回归都要用的公共步骤（搬节点、等落点、藏/收卡面）。
/// ② 调用时机：两段流水线内部。
/// ③ 依赖：本体 <c>NCardFlyVfx</c>（<c>AimFlyAt</c> 用反射改落点）+ <c>GodotWait</c> / <c>NodeLifetime</c>。
/// ④ ⚠️ 反射拿不到 <c>_endPos</c> 时退化为"飞向牌堆落点"（日志会写）；节点判活一律走 <c>NodeLifetime</c>。
/// </remarks>

internal static partial class AeonglassGapSequence
{

    /// <summary>四段演出：抬起 → 开门 → 进门 → 关门（进门的同一刻把间隙镜像挂上）。</summary>
    private static async Task PlayAnimAsync(
        Player player, AeonglassGapPortals portals, List<StagedCard> staged, Vector2 portalCenter)
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
            if (!NodeLifetime.IsAlive(item.Node))
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
            await GodotWait.SecondsAsync(room, FlyHold);
        }

        // ③ 变化 + 进门：**交给本体那套"卡牌飞行特效"**（卡面原地缩小 → 带拖尾，就是卡牌加入牌堆时那个资源）
        await PlayVanillaFlyAsync(ui: room.Ui, staged, player, portalCenter);

        // ④ 收尾：清节点 → 卡面在间隙里出现 → 关门
        //    卡面此刻已经"进了门"（同一时刻才被判为到位）⇒ 先藏起来，剩下那点收尾交给本体特效自己做完。
        HideNodes(staged);
        await GapViewPile.SyncAsync(player);
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
        var fallbackPile = GapViewPile.Zone(player)?.Type ?? PileType.Discard;

        for (var i = 0; i < staged.Count; i++)
        {
            var item = staged[i];
            if (!NodeLifetime.IsAlive(item.Node))
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

            if (flights.All(f => !NodeLifetime.IsAlive(f.Node) || f.Node.GlobalPosition.DistanceTo(f.Target) <= ArriveEpsilon))
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

    /// <summary>
    /// 把这些卡面**藏起来**（不销毁）。它们此刻已经交给本体的飞行特效管了 ——
    /// 特效会在自己结束时连拖尾一起收掉；我们这侧销毁反而会提前掐断它，
    /// 还可能让刚销毁的池实例被下一次 <c>NCard.Create</c> 立刻拿去复用（位置会被旧特效多写一帧）。
    /// </summary>
    private static void HideNodes(List<StagedCard> staged)
    {
        foreach (var item in staged)
        {
            if (NodeLifetime.IsAlive(item.Node))
            {
                item.Node.Visible = false;
            }
        }
    }

    private static void FreeNodes(List<StagedCard> staged)
    {
        foreach (var item in staged)
        {
            if (NodeLifetime.IsAlive(item.Node))
            {
                item.Node.QueueFreeSafely();
            }
        }
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
        var fallbackPile = GapViewPile.Zone(player)?.Type ?? PileType.Discard;

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
            var startScale = UiGeometry.RelativeScale(node, ui);

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
            if (!NodeLifetime.IsAlive(item.Node))
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
}
