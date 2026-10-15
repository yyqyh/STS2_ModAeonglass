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

/// <summary>时空裂隙 动作的返回值（结构化成功/失败原因，方便以后卡牌拿来提示玩家）。</summary>
public readonly record struct GapResult(bool Ok, string Message)
{
    /// <summary>成功。</summary>
    public static GapResult Success(string message) => new(true, message);

    /// <summary>失败（什么都不该发生）。</summary>
    public static GapResult Fail(string message) => new(false, message);
}

/// <summary>
/// 时空裂隙本体：<b>复制手牌 → 本体离场 → 补抽</b>；<b>丢弃手牌 → 把间隙里的牌搬回手牌</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>间隙里放的是「真实卡牌实例」</b>（`CombatState.CloneCard` 造出来的副本，`DeckVersion` 为 null），
/// 直接躺在一个 `RunPersistent` 牌堆里 —— 所以：① 牌堆界面能看见它们；② 搬回手牌即可直接打出；
/// ③ 跨战斗保留（牌堆随 run 走 + 随 run 存档）。
/// </para>
/// <para>
/// 之前那版把它做成了"存档数据槽里的 <c>SerializableCard</c> 列表"，结果牌堆界面永远是空的、
/// 而且补抽前的移除用了 `skipVisuals: true` 导致手牌 UI 留下"幻影"节点。**已按用户的处理逻辑重做**。
/// </para>
/// <para>
/// <b>为什么"代价"堵不住无限循环</b>：花沙砾会等量生成空隙（时之沙的不变式），倒置又能把空隙换回沙砾
    /// ⇒ 沙砾池可循环。所以硬闸只剩下 <b>「必须交替」（<see cref="GapAlternation" />）</b>
    /// —— 每回合激活次数的上限**已按用户 2026-10-14 口径取消**（速率只由交替与代价限）。
/// </para>
/// </remarks>
internal static class GapStore
{
    private static PileType? _gapPileType;
    private static string _zoneId = "-";

    /// <summary>间隙牌堆是否已注册（时空裂隙 关掉时是 false，动作直接拒绝）。</summary>
    public static bool IsReady => _gapPileType is not null;

    /// <summary>
    /// 间隙牌堆的**完整注册 id**（形如 <c>STS2_AEONGLASS_CARDPILE_TIME_GAP</c>）。
    /// </summary>
    /// <remarks>
    /// 给"按 id 找 RitsuLib 的额外手牌容器"用。
    /// ⚠️ 2026-10-12 实测踩坑：一度误用了 `StasisZone.ZoneId`（那是 **搁浅 搁浅**的 id）⇒ 永远找不到容器。
    /// </remarks>
    public static string ZoneId => _zoneId;

    // ============================ 参数（读设置）============================

    /// <summary>间隙容量（设置值 + 玩家的「间隙容量 +N」能力加值，见 <c>AeonglassGapCapacityPower</c>）。</summary>
    public static int Capacity => Math.Clamp(AeonglassSettingStore.Current.GapCapacity, 1, 10);

    /// <summary>某个玩家**实际**能装多少（结构成长卡会放大它）。</summary>
    /// <remarks>容量本身仍是"设置值"；这里额外把该玩家的容量加值算进来（GRW-2 深隙 / GRW-3 摆幅）。</remarks>
    public static int CapacityOf(Player player)
        => Capacity + Core.Content.Powers.AeonglassGapCapacityPower.BonusOf(player);

    /// <summary>副本是否一次性（定档 on）。</summary>
    public static bool OneShot => AeonglassSettingStore.Current.GapOneShot;

    /// <summary>副本是否带 Ethereal（定档 on）。</summary>
    public static bool Ethereal => AeonglassSettingStore.Current.GapEthereal;

    /// <summary>
    /// <b>两个动作的统一定价</b>（沙砾，定档 **2**）：坍缩与唤回各收这一笔（**不按张数乘**）。
    /// </summary>
    /// <remarks>
    /// <para>2026-10-13 用户口径：**统一 2 沙砾**（取代"坍缩 2 / 唤回按张计费"）。</para>
    /// <para>花沙砾会等量生成空隙（时之沙的不变式）⇒ 这条代价同时也是"把沙砾压向空隙侧"的开关。</para>
    /// </remarks>
    public static int ActionCost => Math.Clamp(AeonglassSettingStore.Current.GapActionCost, 0, 6);

    /// <summary>
    /// 该玩家这一次要付的沙砾数：<b>α or β（流逝副）</b>在场且本局还没用过回溯 ⇒ 只付 1 点。
    /// </summary>
    /// <remarks>⚠️ 这是卡池侧标红的"给 时空裂隙 降门槛"那一条（P11 守则 4）；要撤就撤这一个方法。</remarks>
    public static int ActionCostFor(Player player)
        => AlphaBetaPassive.GapCostReduced(player) ? Math.Min(ActionCost, 1) : ActionCost;

    // ============================ 生命周期 ============================

    /// <summary>由 <see cref="ExtraPileModules.RegisterTimeGap" /> 在注册牌堆后调用。</summary>
    public static void Configure(ModCardPileDefinition definition)
    {
        _gapPileType = definition.PileType;
        _zoneId = definition.Id;

        Const.Logger.Info(
            $"[{Const.ModId}] [间隙] 间隙牌堆就绪：{definition.Id}"
            + $"（type=0x{(int)definition.PileType:X8}，scope={definition.Scope}）");
    }

    // ============================ 查询 ============================

    /// <summary>取间隙牌堆（**先解析**：`Player.Piles` 只含已创建的 run 持久牌堆）。</summary>
    public static CardPile? Zone(Player player) =>
        _gapPileType is { } type ? CardPile.Get(type, player) : null;

    /// <summary>间隙里有几张副本。</summary>
    public static int CountOf(Player player) => Zone(player)?.Cards.Count ?? 0;

    /// <summary>一行状态（调试面板 / 日志用）。</summary>
    public static string Describe(Player? player)
    {
        if (player is null)
        {
            return "间隙：无玩家";
        }

        var gap = Zone(player);
        if (gap is null)
        {
            return "间隙牌堆不可用";
        }

        if (gap.Cards.Count == 0)
        {
            return $"间隙 0/{Capacity}：空";
        }

        var names = string.Join(", ", gap.Cards.Select(card => $"{card.Id.Entry}+{card.CurrentUpgradeLevel}"));
        return $"间隙 {gap.Cards.Count}/{Capacity}：{names}";
    }

    // ============================ ① 复制（用户口径）============================

    /// <summary>
    /// 把间隙存储里的实例**直接摘掉**（静默，不走 `CardPileCmd`）。
    /// 显示层那几张克隆随后由 <see cref="GapViewPile.SyncAsync" /> 统一对齐（它会撤掉孤儿克隆）。
    /// </summary>
    internal static int ClearStore(CardPile gap)
    {
        var count = 0;
        foreach (var card in gap.Cards.ToList())
        {
            gap.RemoveInternal(card, true);
            count++;
        }

        return count;
    }

    // ============================ ② 激活（用户口径）============================

    /// <summary>清空间隙（调试用：把里面的实例直接摘掉，不回手牌）。</summary>
    public static GapResult Clear(Player player)
    {
        var gap = Zone(player);
        if (gap is null)
        {
            return GapResult.Fail("时空裂隙 未启用");
        }

        var count = gap.Cards.Count;
        foreach (var card in gap.Cards.ToList())
        {
            gap.RemoveInternal(card, true);
        }

        gap.InvokeContentsChanged();
        MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(GapViewPile.SyncAsync(Players_FirstOrNull()));
        Core.Presentation.Combat.CombatUiRefresh.RefreshAll(Players_FirstOrNull());
        return GapResult.Success($"间隙已清空（原有 {count} 张）");
    }

    /// <summary>清空这条调试路径没有玩家参数，从当前战斗里找一个（找不到就返回 null）。</summary>
    private static MegaCrit.Sts2.Core.Entities.Players.Player? Players_FirstOrNull() =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()?.Players.FirstOrDefault();

}
