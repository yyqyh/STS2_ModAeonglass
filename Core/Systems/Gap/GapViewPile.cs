using System.Diagnostics;

using Godot;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2RitsuLib.CardPiles;
using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Presentation.Support;
using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>间隙的<b>显示层镜像</b>：把「间隙」存储牌堆镜像到一个 <c>ExtraHand</c> 显示牌堆。</summary>
/// <remarks>
/// <b>【留档未启用】</b>2026-10-16 试过"存储 = 显示"的单牌堆合并（见 <c>ExtraPileModules.MergeGapPiles</c>），
/// 被本体断言否决（非战斗牌堆的卡不能做多人悬停序列化）⇒ 开关已关回 <c>false</c>，本文件的
/// <c>ConfigureMerged</c> / <c>_merged</c> 分支是**留档路径**（要重试先看那个开关的注释）。
/// ① 职责：建显示牌堆、把存储内容镜像过来（走 <c>CardPileCmd</c>）、对外报状态（<c>IsReady</c> / <c>ZoneId</c>）。
/// ② 挂点与时机：由 <c>ExtraPileModules.RegisterTimeGap</c> 在注册显示牌堆后调用；此后每次内容变化再镜像。
/// ③ 依赖：RitsuLib <c>CardPiles</c>、<c>GapStore</c>（内容真相）。
/// ④ ⚠️ 用户 2026-10-12 定的"存储与显示分离"：存储牌堆（<c>time_gap</c>，RunPersistent + Headless）是真相、随存档序列化、不显示；
/// 显示牌堆（<c>time_gap_view</c>，CombatOnly + ExtraHand）只是投影、每场战斗重建。RitsuLib 的额外手牌<b>不订阅牌堆事件</b>
/// （卡面靠原版可视流程挂）⇒ 镜像必须走 <c>CardPileCmd</c> 才看得见；链接关系用 <c>clone.DeckVersion = 存储卡</c>。
/// </remarks>
internal static class GapViewPile
{
    private static PileType? _viewPileType;
    private static string _viewId = "-";

    /// <summary>是否处于"单牌堆合并"模式（存储 = 显示，没有镜像层可同步）。</summary>
    private static bool _merged;

    private static bool _mergedLogged;

    /// <summary>显示牌堆是否已注册。</summary>
    public static bool IsReady => _viewPileType is not null;

    /// <summary>是否处于"单牌堆合并"模式（存储 = 显示）。</summary>
    public static bool IsMerged => _merged;

    /// <summary>显示牌堆的完整注册 id（给"按 id 找额外手牌容器"用）。</summary>
    public static string ZoneId => _viewId;

    /// <summary>由 <see cref="ExtraPileModules.RegisterTimeGap" /> 在注册显示牌堆后调用。</summary>
    public static void Configure(ModCardPileDefinition definition)
    {
        _viewPileType = definition.PileType;
        _viewId = definition.Id;
    }

    /// <summary>
    /// 由 <see cref="ExtraPileModules.RegisterTimeGap" /> 的**单牌堆模式**调用：显示侧直接指向那张牌堆。
    /// </summary>
    /// <remarks>
    /// 合并后 <c>ZoneId</c> / <c>Zone(player)</c> 依旧可用（同一张 <c>RunPersistent + ExtraHand</c> 牌堆），
    /// 只有 <see cref="SyncAsync" /> 变成空转 —— 这样表现层那 16 处调用点一行都不用改。
    /// </remarks>
    public static void ConfigureMerged(ModCardPileDefinition definition)
    {
        _viewPileType = definition.PileType;
        _viewId = definition.Id;
        _merged = true;
    }

    /// <summary>显示牌堆（没注册 / 玩家为空时为 null）。</summary>
    public static CardPile? Zone(Player? player) =>
        player is not null && _viewPileType is { } type ? CardPile.Get(type, player) : null;

    /// <summary>
    /// <b>合并模式</b>下把某张牌的卡面从额外手牌里摘掉（尽力而为：找不到就跳过）。
    /// </summary>
    /// <remarks>
    /// 单牌堆模式下"静默移出牌堆"不会自己撤卡面（RitsuLib 的额外手牌只在挂堆 / `CardPileCmd` 流程里建撤卡面）
    /// ⇒ 调试用的"清空间隙"这类静默路径要显式摘一下。正常战斗路径不受影响（挪到手牌 / 弃牌堆时走 `CardPileCmd`）。
    /// </remarks>
    internal static void DetachVisual(CardModel card)
    {
        if (!_merged)
        {
            return;
        }

        try
        {
            if (FindContainer()?.GetCard(card) is not { } node || !GodotObject.IsInstanceValid(node))
            {
                return;
            }

            node.GetParent()?.RemoveChild(node);
            node.QueueFree();
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Gap} 摘间隙卡面 {card.Id.Entry} 失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>找我们的额外手牌容器（按定义 id 匹配；不在战斗里返回 null）。</summary>
    internal static NModExtraHand? FindContainer()
    {
        if (NCombatRoom.Instance?.Ui is not { } ui)
        {
            return null;
        }

        return NodeSearch.FindDescendant<NModExtraHand>(
            ui,
            hand => string.Equals(hand.Definition?.Id, ZoneId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>显示牌堆里的克隆（按 <c>DeckVersion</c> 反查）。</summary>
    private static CardModel? CloneOf(CardPile view, CardModel stored) =>
        view.Cards.FirstOrDefault(c => ReferenceEquals(c.DeckVersion, stored));

    /// <summary>
    /// 把显示牌堆同步成"存储牌堆的投影"（幂等；每次内容变化后调一次）。
    /// </summary>
    /// <remarks>
    /// 增：缺的克隆 → <c>combat.CloneCard</c> + <c>CardPileCmd.Add</c>（走原版可视流程，卡面当场挂上）。
    /// 删：孤立的克隆 → <c>CardPileCmd.RemoveFromCombat</c>（显示堆是 <c>CombatOnly</c>，
    /// RitsuLib 的补丁让它算战斗牌堆 ⇒ 这条路可用，卡面当场撤掉）。
    /// </remarks>
    public static async Task SyncAsync(Player? player)
    {
        if (_merged)
        {
            if (!_mergedLogged)
            {
                _mergedLogged = true;
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 镜像层已合并（单牌堆模式）⇒ 不再做存储 → 显示的同步");
            }

            return;
        }

        if (player is null || !IsReady || !GapStore.IsReady)
        {
            return;
        }

        // 性能探针：合并模式已在上面的早退里返回，这里量的是"真做同步"的那段（默认只在开关打开时报）。
        var watch = Stopwatch.StartNew();

        var view = Zone(player);
        var stored = GapStore.Zone(player);
        if (view is null || stored is null)
        {
            return;
        }

        if (player.Creature.CombatState is not CombatState combat)
        {
            return;
        }

        // ① 补缺：存储里有、显示里没有的 → 克隆一份挂进显示牌堆（走 CardPileCmd ⇒ 有卡面）。
        foreach (var card in stored.Cards.ToList())
        {
            if (CloneOf(view, card) is not null)
            {
                continue;
            }

            try
            {
                var clone = combat.CloneCard(card);
                clone.DeckVersion = card; // 链接：这份克隆是"哪一张存储卡的投影"
                var result = await CardPileCmd.Add(
                    clone, view, CardPilePosition.Bottom, null, skipVisuals: false);

                if (!result.success)
                {
                    Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Gap} 镜像 {card.Id.Entry} 失败（success=false）");
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 镜像 {card.Id.Entry} 抛异常：{ex.GetType().Name}: {ex.Message}");
            }
        }

        // ② 清理：显示里残留、存储里已经没有的克隆 → 撤掉。
        //
        // ⚠️ **只撤模型**：`skipVisuals: true` 跳过本体的动画 / 节点段（2026-10-16 定）。
        //    ① 观感不需要它：这 5 张早由表现层"原地透明化"淡掉了，紧接着的
        //       `CombatUiRefresh.RefreshAll → ExtraHandRefresh.Nudge → NModExtraHand.Initialize`
        //       会把额外手牌区**按显示堆现在的内容重建**（`AttachPile` 先清 holder 再按 `_pile.Cards` 造卡面）
        //       ⇒ 卡面自己就收拾干净了。
        //    ② 带着视觉段会炸：本体 `RemoveFromCombat` 的视觉段拿 `oldPiles[节点当时的 Model]` 反查
        //       （key 打印成 `CARD.<entry> (<RuntimeHelpers.GetHashCode>)` = `AbstractModel.ToString()`），
        //       而我们的槽位卡节点会被卡面池复用/换绑到别的卡 ⇒ 查不到、抛 KeyNotFoundException，
        //       一次撤堆刷 5 条警告（实测 log 982-986，端状态本来就对）。
        var storedSet = stored.Cards.ToHashSet();
        foreach (var clone in view.Cards.ToList())
        {
            if (clone.DeckVersion is { } master && storedSet.Contains(master))
            {
                continue;
            }

            // 已在别处被摘掉的克隆：不要再去撤一次。
            // （历史坑：额外手牌刷新会先把它摘走 ⇒ CardPileCmd.RemoveFromCombat 抛 KeyNotFoundException，
            //  一次撤堆刷 5 条警告；端状态本来就对，所以这里降级成 Info 并跳过。）
            if (!view.Cards.Contains(clone))
            {
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 镜像 {clone.Id.Entry} 已不在显示堆里，跳过撤下");
                continue;
            }

            try
            {
                await CardPileCmd.RemoveFromCombat(clone, skipVisuals: true);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 撤下镜像 {clone.Id.Entry} 抛异常：{ex.GetType().Name}: {ex.Message}");
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Gap} 间隙镜像已同步：存储 {stored.Cards.Count} 张 → 显示 {view.Cards.Count} 张");

        // 性能探针（默认关，只跟"详细诊断日志"开关走）：同步耗时 —— 坍缩/唤回每次都跑这段，
        // 以后要收窄成"只同步变化的那几张"前，先看这里的数据。
        if (AeonglassSettingStore.DebugVerboseLogs)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Gap} 镜像同步耗时 {watch.Elapsed.TotalMilliseconds:0.##} ms"
                + $"（存储 {stored.Cards.Count} → 显示 {view.Cards.Count}）");
        }
    }
}
