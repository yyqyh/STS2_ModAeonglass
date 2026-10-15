using Godot;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Presentation.Support;

using STS2_Aeonglass.Core.Systems.Gap;
namespace STS2_Aeonglass.Core.Presentation.Combat;
/// <summary>间隙的「主动刷新」入口：让<b>额外手牌容器</b>重新同步一次。</summary>
/// <remarks>
/// ① 职责：按显示牌堆 id 找容器，重排/压层级/恢复鼠标穿透。
/// ② 调用时机：每次 时空裂隙 动作（坍缩 / 回归 / 清空）之后。
/// ③ 依赖：<c>NodeSearch</c>（按 <c>GapViewPile.ZoneId</c> 找 <c>NModExtraHand</c>）+ holder 的 Hitbox 规则。
/// ④ ⚠️ 搜索深度上限 6：RitsuLib 改了容器挂点层级就会<b>静默失效</b>；找不到时日志会写。
/// </remarks>

internal static class ExtraHandRefresh
{
    /// <summary>
    /// 当前是否已经把额外手牌区的 holder 设成"鼠标穿透"（缓存，避免每帧重复扫描）。
    /// </summary>
    private static bool _mouseThrough;

    /// <summary>
    /// 目标选取期间让**额外手牌区（间隙显示区）不挡鼠标**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 问题（群友 2026-10-13 指出）：RitsuLib 的额外手牌里，每个 <c>NHandCardHolder</c> 的
    /// <c>Hitbox.MouseFilter</c> **一直是 <c>Stop</c>**；而它们不在 <c>NPlayerHand.ActiveHolders</c> 名单里，
    /// 所以拿不到原版手牌那套"拖动时切 <c>Ignore</c>"（<c>HasDraggedHolder ? 2 : 0</c>）的自动穿透。
    /// 于是当额外手牌区的卡压在怪物（或别的可点目标）身上时，鼠标停在卡上就 hover 不到下面。
    /// </para>
    /// <para>
    /// 做法：**只在目标选取期间**把那些 holder 的 Hitbox 切 <c>Ignore</c>，选完恢复 <c>Stop</c>；
    /// 状态用 <see cref="_mouseThrough" /> 缓存，只在变化时扫一次（平时每帧只是一次 bool 比较）。
    /// </para>
    /// </remarks>
    public static void SyncMouseThrough()
    {
        var through = NTargetManager.Instance is { IsInSelection: true };
        if (through == _mouseThrough)
        {
            return;
        }

        if (SetMouseThrough(through))
        {
            _mouseThrough = through;
        }
    }

    /// <summary>把额外手牌区所有 holder 的 Hitbox 设成穿透 / 恢复；返回是否真的动到了（没找到容器时 false）。</summary>
    private static bool SetMouseThrough(bool through)
    {
        try
        {
            if (NCombatRoom.Instance?.Ui is not { } combatUi)
            {
                return false;
            }

            var target = GapViewPile.ZoneId;
            var scanned = 0;
            if (NodeSearch.FindDescendant<NModExtraHand>(combatUi, hand => string.Equals(hand.Definition?.Id, target, StringComparison.OrdinalIgnoreCase), NodeSearch.DefaultMaxDepth, ref scanned) is not { } hand)
            {
                return false;
            }

            var count = ApplyToHolders(hand, through, 0);
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Gap} 额外手牌鼠标穿透={(through ? "开（目标选取中）" : "关")}："
                + $"{count} 个 holder");
            return true;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Gap} 切换额外手牌鼠标穿透失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>递归把容器下所有 <c>NHandCardHolder</c> 的 Hitbox 切到目标状态；返回处理了几个。</summary>
    private static int ApplyToHolders(Node node, bool through, int depth)
    {
        if (depth > 6)
        {
            return 0;
        }

        var count = 0;
        foreach (var child in node.GetChildren())
        {
            if (child is NHandCardHolder holder)
            {
                holder.Hitbox.MouseFilter = through
                    ? Control.MouseFilterEnum.Ignore
                    : Control.MouseFilterEnum.Stop;
                count++;
            }

            count += ApplyToHolders(child, through, depth + 1);
        }

        return count;
    }

    /// <summary>把额外手牌容器重新同步一次（找不到容器就静默跳过）。</summary>
    public static void Nudge(Player? player)
    {
        if (player is null)
        {
            return;
        }

        try
        {
            if (NCombatRoom.Instance?.Ui is not { } combatUi)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 额外手牌刷新：拿不到战斗 UI（跳过）");
                return;
            }

            // ⚠️ 这里必须是**显示牌堆**（ExtraHand 容器）的 id。
            // 踩过的两个坑：① 写成 StasisStore.ZoneId（= 搁浅 搁浅）⇒ 匹配不上；
            // ② 写成 GapStore.ZoneId（= 存储牌堆，Headless 无容器）⇒ 也匹配不上。
            var target = GapViewPile.ZoneId;
            var scanned = 0;
            if (NodeSearch.FindDescendant<NModExtraHand>(combatUi, hand => string.Equals(hand.Definition?.Id, target, StringComparison.OrdinalIgnoreCase), NodeSearch.DefaultMaxDepth, ref scanned) is { } hand)
            {
                // 「在手牌区后面」= **层级**在后面（用户 2026-10-11 选的方案 1）：
                // 把容器自身的 z 压到 0 以下 ⇒ 与手牌重叠的部分会被手牌盖住。
                // （容器是 NCombatUi 的直接子节点，ZIndex 默认相对父节点生效。）
                if (hand.ZIndex >= 0)
                {
                    hand.ZIndex = -1;
                }

                hand.Initialize(player);
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 额外手牌刷新：已找到容器并重新同步（id={target}，z={hand.ZIndex}）");
                return;
            }

            // 找不到也要留痕：否则"没刷新"和"没执行"从日志上无法区分（实测踩过）。
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Gap} 额外手牌刷新：**没找到容器**（扫了 {scanned} 个节点，目标 id={target}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Gap} 额外手牌主动刷新失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }

}
