using Godot;

using MegaCrit.Sts2.Core.Helpers;

using STS2RitsuLib;
using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;

using STS2_Aeonglass.Core.Systems.Stasis;
using STS2_Aeonglass.Core.Systems.Gap;

namespace STS2_Aeonglass.Core.Systems.Bootstrap;

/// <summary>
/// 额外牌堆（时空裂隙 / 搁浅）的<b>注册件</b>。
/// </summary>
/// <remarks>
/// ① 职责：把两张额外牌的牌堆定义注册进本体。
/// ② 调用时机：机制注册；牌堆可见性按设置。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class ExtraPileModules
{
    /// <summary>日志 / 设置页里显示的机制名。</summary>
    public const string StasisDisplayName = "搁浅";

    /// <inheritdoc cref="StasisDisplayName" />
    public const string TimeGapDisplayName = "时空裂隙";

    /// <summary>搁浅 是否启用。</summary>
    public static bool StasisEnabled => AeonglassSettingStore.Current.StasisEnabled;

    /// <summary>时空裂隙 是否启用。</summary>
    public static bool TimeGapEnabled => AeonglassSettingStore.Current.GapEnabled;

    // ============================ 搁浅 ============================

    /// <summary>注册 搁浅的搁浅牌堆（右上角"牌组"按钮之后）。</summary>
    public static void RegisterStasis()
    {
        var zone = RitsuLibFramework.GetCardPileRegistry(Const.ModId).RegisterOwned(
            Const.Paths.CardPiles.EmptyZoneStem,
            new ModCardPileSpec
            {
                Scope = ModCardPileScope.RunPersistent,
                Style = ModCardPileUiStyle.TopBarDeck,
                Anchor = new ModCardPileAnchor(ModCardPileAnchorKind.TopBarAfterDeck),
                IconPath = Const.Paths.Ui.PileIcon(Const.Paths.CardPiles.EmptyZoneStem),
                // 角色门：别的角色（含本体）的界面上不出现这两个牌堆按钮。
                VisibleWhen = ctx => ctx.Player != null && Systems.Bootstrap.CharacterGate.IsActive,
                // OnOpen 留空 ⇒ 点开就是原版牌堆界面（只读查看）。
            });

        StasisStore.Configure(zone);
    }

    // ============================ 时空裂隙 ============================

    /// <summary>
    /// 注册 间隙的间隙牌堆（<b>额外手牌容器</b>形态）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 间隙里放的是**真实卡牌实例**，靠牌堆自己的 `RunPersistent` 作用域跨战斗保留
    /// —— 所以**不需要**再额外注册一份 <c>RunSavedDataStore</c> 数据槽（那是上一版的设计，已废弃）。
    /// </para>
    /// <para>
    /// <b>2026-10-11 改为 ExtraHand 形态</b>（用户口径：「我需要真实可以看到的卡牌实例 …
    /// 是一个可用聚焦卡牌的『手牌』但**不能打出**」）：用 RitsuLib 的额外手牌容器，
    /// 锚点 <c>ExtraHandAbove</c> = 居中放在原版手牌上方（排版/跟随由框架负责，不再自己算坐标），
    /// 卡牌一律用**真卡面节点**、<c>AllowCardPlay = false</c>、不显示可打出高光。
    /// </para>
    /// </remarks>
    public static void RegisterTimeGap()
    {
        // ① **存储牌堆**（真相所在）：Headless（不显示）+ RunPersistent（随存档序列化）。
        //    存进来的真实例只在这里；显示靠下面的"镜像"，所以这里不需要任何 UI 参数。
        var gap = RitsuLibFramework.GetCardPileRegistry(Const.ModId).RegisterOwned(
            Const.Paths.CardPiles.TimeGapStem,
            new ModCardPileSpec
            {
                Scope = ModCardPileScope.RunPersistent,
                Style = ModCardPileUiStyle.Headless,
            });

        // ② **显示牌堆**（投影）：战斗内存活（CombatOnly，不进存档）+ ExtraHand 容器。
        //    内容由 `GapViewPile.SyncAsync` 用 `CardPileCmd` 从存储镜像过来 —— 卡面因此走原版可视流程。
        //    ⚠️ 下面这套表现参数是**用户 2026-10-12 亲自调过的**（手牌下方 / 灰色禁用态 / 显示可打出高光），不要改回默认。
        var view = RitsuLibFramework.GetCardPileRegistry(Const.ModId).RegisterOwned(
            Const.Paths.CardPiles.TimeGapViewStem,
            new ModCardPileSpec
            {
                Scope = ModCardPileScope.CombatOnly,
                // 额外手牌容器：居中放在原版手牌下方。
                Style = ModCardPileUiStyle.ExtraHand,
                Anchor = new ModCardPileAnchor(ModCardPileAnchorKind.ExtraHandBelow),
                // 不能打出 ⇒ 必须显式要求"用卡牌节点表示"（否则只有一个空容器）。
                CardShouldBeVisible = true,
                ExtraHand = new ModCardPileExtraHandSpec
                {
                    AllowCardPlay = false,
                    ShowPlayableGlow = true,
                    // 而禁用态的默认表现是 **下移 100px + 灰度 50%** ⇒ 卡面被推出屏幕、看起来"没出现"。
                    // 我们要求的是"能看能聚焦、只是不能打出"，所以把禁用表现**中和掉**：
                    DisabledOffset = new(0,240),
                    DisabledModulate = Colors.SlateGray,
                    // 过渡时长**用它自带的**（0.2s，与本体手牌一致）——用户在 2026-10-11 明确要"使用它的"。
                    // 因为偏移是 0、颜色是白，这次过渡在视觉上是无感的，只是把属性交给框架管。
                    DisabledTransitionDuration = 0.2,
                    // 卡面挂载后 / 入场动画到位后：先只记账（下一步的传送门与飞行特效把动画挂在这两个点上）。
                    OnCardVisualCreated = ctx => Const.Logger.Info(
                        $"[{Const.ModId}] {Const.Prefix.Gap} 额外手牌：卡面已挂载 {ctx.Card?.Id.Entry ?? "-"}"),
                    OnCardArrived = ctx => Const.Logger.Info(
                        $"[{Const.ModId}] {Const.Prefix.Gap} 额外手牌：入场到位 {ctx.Card?.Id.Entry ?? "-"}"),
                },
                IconPath = Const.Paths.Ui.PileIcon(Const.Paths.CardPiles.TimeGapStem),
                VisibleWhen = ctx => ctx.Player != null && Systems.Bootstrap.CharacterGate.IsActive,
            });

        GapViewPile.Configure(view);
        GapStore.Configure(gap);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Gap} 间隙牌堆已注册：存储 {gap.Id}（Headless+RunPersistent）"
            + $" → 显示 {view.Id}（ExtraHand@手牌下方+CombatOnly，可见卡面=是，可打出=否）");

        // ③ 每场战斗开始时把显示重建一次（CombatOnly 牌堆每场都是空的）。
        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(_ =>
        {
            try
            {
                var players = MegaCrit.Sts2.Core.Combat.CombatManager.Instance
                    .DebugOnlyGetState()?.Players ?? [];
                foreach (var player in players)
                {
                    TaskHelper.RunSafely(GapViewPile.SyncAsync(player));
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Gap} 战斗开始同步镜像失败（已忽略）：{ex.GetType().Name}");
            }
        });
    }
}
