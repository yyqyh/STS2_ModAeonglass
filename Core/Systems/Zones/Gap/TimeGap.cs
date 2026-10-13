using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Systems.Zones;

/// <summary>M2 动作的返回值（结构化成功/失败原因，方便以后卡牌拿来提示玩家）。</summary>
public readonly record struct GapResult(bool Ok, string Message)
{
    /// <summary>成功。</summary>
    public static GapResult Success(string message) => new(true, message);

    /// <summary>失败（什么都不该发生）。</summary>
    public static GapResult Fail(string message) => new(false, message);
}

/// <summary>
/// M2「时空间隙」本体：<b>复制手牌 → 本体离场 → 补抽</b>；<b>丢弃手牌 → 把间隙里的牌搬回手牌</b>。
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
/// <b>为什么"代价"堵不住无限循环</b>：花沙砾会等量生成空隙（M1 的不变式），倒置又能把空隙换回沙砾
/// ⇒ 沙砾池可循环。所以硬闸只有两条：**副本一次性**（本类）与**每回合激活上限**（<see cref="GapTurnLimit" />）。
/// </para>
/// </remarks>
internal static class TimeGap
{
    private static PileType? _gapPileType;
    private static string _zoneId = "-";

    /// <summary>间隙牌堆是否已注册（M2 关掉时是 false，动作直接拒绝）。</summary>
    public static bool IsReady => _gapPileType is not null;

    /// <summary>
    /// 间隙牌堆的**完整注册 id**（形如 <c>STS2_AEONGLASS_CARDPILE_TIME_GAP</c>）。
    /// </summary>
    /// <remarks>
    /// 给"按 id 找 RitsuLib 的额外手牌容器"用。
    /// ⚠️ 2026-10-12 实测踩坑：一度误用了 `StasisZone.ZoneId`（那是 **M7 搁浅**的 id）⇒ 永远找不到容器。
    /// </remarks>
    public static string ZoneId => _zoneId;

    // ============================ 参数（读设置）============================

    /// <summary>间隙容量（定档 5）。</summary>
    public static int Capacity => Math.Clamp(AeonglassSettingStore.Current.GapCapacity, 1, 10);

    /// <summary>副本是否一次性（定档 on）。</summary>
    public static bool OneShot => AeonglassSettingStore.Current.GapOneShot;

    /// <summary>副本是否带 Ethereal（定档 on）。</summary>
    public static bool Ethereal => AeonglassSettingStore.Current.GapEthereal;

    /// <summary>
    /// <b>两个动作的统一定价</b>（沙砾，定档 **2**）：坍缩与唤回各收这一笔（**不按张数乘**）。
    /// </summary>
    /// <remarks>
    /// <para>2026-10-13 用户口径：**统一 2 沙砾**（取代"坍缩 2 / 唤回按张计费"）。</para>
    /// <para>花沙砾会等量生成空隙（M1 的不变式）⇒ 这条代价同时也是"把沙砾压向空隙侧"的开关。</para>
    /// </remarks>
    public static int ActionCost => Math.Clamp(AeonglassSettingStore.Current.GapActionCost, 0, 6);

    /// <summary>每回合激活次数上限（定档 1，硬闸）—— **坍缩与回归共用这一个额度**。</summary>
    public static int ActivationsPerTurn =>
        Math.Clamp(AeonglassSettingStore.Current.GapActivationsPerTurn, 1, 5);

    // ============================ 生命周期 ============================

    /// <summary>由 <see cref="ZonesModule.RegisterTimeGap" /> 在注册牌堆后调用。</summary>
    public static void Configure(ModCardPileDefinition definition)
    {
        _gapPileType = definition.PileType;
        _zoneId = definition.Id;

        Const.Logger.Info(
            $"[{Const.ModId}] [M2] 间隙牌堆就绪：{definition.Id}"
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
    /// <b>预测</b>：这一次「坍缩」会搬走手牌里的哪几张（**无任何副作用**，只回答"会飞哪几张"）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 给表现层用（<c>GapSequence</c>：它要先知道"演什么"，才能去抓对应的卡节点）。
    /// 返回空 = 这次一定不成功（不在战斗 / M2 没注册 / 本场次数用完 / 间隙已满 / 手牌为空）⇒
    /// 表现层<b>什么都不该演</b>，直接走 <see cref="Activate" /> 拿失败原因。
    /// </para>
    /// <para>
    /// <b>预测与执行共用 <see cref="PlanCore" /> 这段规则</b>，所以不会出现"演的和做的不一样"。
    /// </para>
    /// </remarks>
    public static IReadOnlyList<CardModel> PlanCollapse(Player player)
    {
        if (player.PlayerCombatState is not { } piles || Zone(player) is null)
        {
            return [];
        }

        // **预测必须与执行端一致**（2026-10-14 的两道闸 + 坍缩代价）：任一不满足就"什么都不该演"。
        if (GapTurnLimit.UsedThisTurn(player) >= ActivationsPerTurn
            || !GapTurnLimit.AllowsAlternation(player, GapTurnLimit.GapAction.Collapse)
            || ActionCost > SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId))
        {
            return [];
        }

        // 坍缩现在**先清空间隙**（用户 2026-10-12 口径）⇒ 容量不再是门槛：整手牌都能进去。
        return PlanCore(piles.Hand, Capacity);
    }

    /// <summary>规则本体：手牌里**前 <paramref name="room" /> 张**（照手牌顺序，执行端逐字一致）。</summary>
    private static IReadOnlyList<CardModel> PlanCore(CardPile hand, int room) =>
        room <= 0 ? [] : hand.Cards.Take(room).ToList();

    /// <summary>
    /// 把间隙存储里的实例**直接摘掉**（静默，不走 `CardPileCmd`）。
    /// 显示层那几张克隆随后由 <see cref="GapView.SyncAsync" /> 统一对齐（它会撤掉孤儿克隆）。
    /// </summary>
    private static int ClearStore(CardPile gap)
    {
        var count = 0;
        foreach (var card in gap.Cards.ToList())
        {
            gap.RemoveInternal(card, true);
            count++;
        }

        return count;
    }

    /// <summary>
    /// 复制：<b>找到手牌 → 复制实例进间隙 → 真实移出手牌 → 从抽牌堆补抽等量</b>。
    /// </summary>
    /// <param name="player">做动作的玩家。</param>
    /// <param name="deferMirror">
    /// <c>true</c> = <b>不</b>立刻把间隙镜像挂到显示牌堆上，交给表现层在「卡面进传送门」的那一刻自己调
    /// <see cref="GapView.SyncAsync" />（否则间隙里的卡面会在动画刚开头就冒出来）；默认 <c>false</c> = 老行为。
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
            Const.Logger.Info($"[{Const.ModId}] [M2] 复制被拒：不在战斗中");
            return GapResult.Fail("不在战斗中");
        }

        var gap = Zone(player);
        if (gap is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [M2] 复制被拒：M2 未启用（间隙牌堆没注册）");
            return GapResult.Fail("M2 未启用（间隙牌堆没注册）");
        }

        // 空手牌 ⇒ 整次不动（**必须在付代价之前**：否则会"扣了 2 沙砾却什么都没搬"）。
        if (combatPiles.Hand.Cards.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] [M2] 复制被拒：手牌为空（没有可复制的牌）");
            return GapResult.Fail("手牌为空，没有可复制的牌");
        }

        // —— 2026-10-14 规则层定档：两道闸 + 坍缩代价（不足则整次不动）——
        var used = GapTurnLimit.UsedThisTurn(player);
        if (used >= ActivationsPerTurn)
        {
            var blocked = $"坍缩被拒（本回合已激活 {used}/{ActivationsPerTurn}）";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} {blocked}");
            return GapResult.Fail(blocked);
        }

        if (!GapTurnLimit.AllowsAlternation(player, GapTurnLimit.GapAction.Collapse))
        {
            var blocked = "坍缩被拒（坍缩与回归必须交替：先回归一次）";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} {blocked}");
            return GapResult.Fail(blocked);
        }

        if (ActionCost > 0
            && !await SecondaryResourceCmd.Spend(player, AeonglassSecondaryResources.SandId, ActionCost))
        {
            var have = SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId);
            var blocked = $"坍缩被拒（沙砾不足：需要 {ActionCost}，现有 {have}）";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} {blocked}");
            return GapResult.Fail(blocked);
        }

        // ★ 用户口径（2026-10-12）：「坍缩的时候，要先清理间隙」——
        //   先把存储里旧的副本清掉，再复制手牌。于是间隙里永远是"最近一次坍缩的那一手"，
        //   不会新旧混着，也不再因为装不下而整次失败（容量不再是门槛）。
        //   显示层那几张旧卡面由表现层（GapSequence）在动画开头淡出，这里只动存储。
        var cleared = ClearStore(gap);
        if (cleared > 0)
        {
            Const.Logger.Info($"[{Const.ModId}] [M2] 坍缩：先清空间隙（原有 {cleared} 张）");
        }

        var room = Capacity;
        var hand = combatPiles.Hand;
        var handBefore = hand.Cards.Count;
        var plan = PlanCore(hand, room);

        // ① 复制实例 → ② 进间隙（真实实例，牌堆界面可见、可跨战斗）→ ③ 真实移出手牌。
        foreach (var card in plan)
        {
            var copy = combat.CloneCard(card);
            // 存储牌堆是 **Headless** 的 ⇒ 这里静默写就行（显示由 GapView 镜像负责）。
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
            await GapView.SyncAsync(player);
        }

        // 兜底再全刷一遍（牌组 / 战斗五堆 / 模组牌堆 / 容器）。
        gap.InvokeContentsChanged();
        Core.Presentation.UI.CombatUiRefresh.RefreshAll(player);

        // ④ 补抽（**Step 5 起默认取消**）：新口径下坍缩的代价就是"这回合手牌变少"，
        //    不再从抽牌堆补回来。设置里的 mechanism_m2_refill_draw 可以开回旧行为。
        var drawCount = Math.Min(handBefore, copied);
        var drawn = 0;
        if (AeonglassSettingStore.Current.GapRefillDraw)
        {
            drawn = (await CardPileCmd.Draw(new BlockingPlayerChoiceContext(), drawCount, player)).Count();
        }

        GapTurnLimit.Mark(player, GapTurnLimit.GapAction.Collapse);

        var text = $"坍缩：手牌 {handBefore} 张 → 间隙 +{copied}（{CountOf(player)}/{Capacity}）"
                   + $" → 移出手牌 {copied}"
                   + (AeonglassSettingStore.Current.GapRefillDraw ? $" → 补抽 {drawn}/{drawCount}" : " → 不补抽");
        Const.Logger.Info($"[{Const.ModId}] [M2] {text}");
        return GapResult.Success(text);
    }

    // ============================ ② 激活（用户口径）============================

    /// <summary>
    /// <b>预测</b>：这一次「回归」会把间隙里的哪几张搬回手牌（**无任何副作用**）。
    /// </summary>
    /// <remarks>
    /// 返回空 = 这次一定不成功（不在战斗 / 间隙没注册 / 间隙是空的 / 沙砾不够付代价）⇒
    /// 表现层什么都不该演，直接走 <see cref="Recall" /> 拿失败原因。
    /// 与执行端共用"间隙里现在有什么、代价多少"这两条判断，不会出现"演的和做的不一样"。
    /// </remarks>
    public static IReadOnlyList<CardModel> PlanRecall(Player player)
    {
        if (player.PlayerCombatState is null || Zone(player) is not { } gap || gap.Cards.Count == 0)
        {
            return [];
        }

        // **与执行端同源**：两道闸（每回合额度 + 必须交替）任一不满足 ⇒ 这一次一定失败 ⇒ 什么都不该演。
        if (GapTurnLimit.UsedThisTurn(player) >= ActivationsPerTurn
            || !GapTurnLimit.AllowsAlternation(player, GapTurnLimit.GapAction.Recall))
        {
            return [];
        }

        // 定价与坍缩同一笔（2026-10-13 统一 2 沙砾，不再按张乘）。
        var cost = ActionCost;
        if (cost > 0 && SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId) < cost)
        {
            return [];
        }

        return gap.Cards.ToList();
    }

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
    /// <b>两道速率闸先过</b>（2026-10-14 定档，见 <see cref="GapTurnLimit" />）：本回合额度用尽、或与上一次动作没有交替
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
            Const.Logger.Info($"[{Const.ModId}] [M2] 激活被拒：不在战斗中");
            return GapResult.Fail("不在战斗中");
        }

        var gap = Zone(player);
        if (gap is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [M2] 激活被拒：M2 未启用（间隙牌堆没注册）");
            return GapResult.Fail("M2 未启用（间隙牌堆没注册）");
        }

        // —— 2026-10-14 规则层定档：回归与坍缩共用同一道速率闸 + 必须交替 ——
        //    ⚠️ 这两道检查必须在**付代价 / 搬牌之前**：不满足就整次不动（不能出现"手牌弃了、副本没回来"）。
        var used = GapTurnLimit.UsedThisTurn(player);
        if (used >= ActivationsPerTurn)
        {
            var blocked = $"回归被拒（本回合已激活 {used}/{ActivationsPerTurn}）";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} {blocked}");
            return GapResult.Fail(blocked);
        }

        if (!GapTurnLimit.AllowsAlternation(player, GapTurnLimit.GapAction.Recall))
        {
            var blocked = "回归被拒（坍缩与回归必须交替：先坍缩一次）";
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} {blocked}");
            return GapResult.Fail(blocked);
        }

        if (gap.Cards.Count == 0)
        {
            // 这条以前是"静默失败"（不打日志），实测时最难判断 —— 现在必须留痕。
            Const.Logger.Info($"[{Const.ModId}] [M2] 激活被拒：间隙是空的（{Describe(player)}）");
            return GapResult.Fail("间隙是空的");
        }

        var copies = gap.Cards.ToList();
        Const.Logger.Info($"[{Const.ModId}] [M2] 激活开始：{Describe(player)}");

        // ① 先付代价：不足就整次不动（与坍缩同一笔：统一 2 沙砾）。
        var cost = ActionCost;
        if (cost > 0 && !await SecondaryResourceCmd.Spend(player, AeonglassSecondaryResources.SandId, cost))
        {
            var have = SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId);
            var blocked = $"激活被拒（沙砾不足：需要 {cost}，现有 {have}）";
            Const.Logger.Info($"[{Const.ModId}] [M2] {blocked}");
            return GapResult.Fail(blocked);
        }

        // ② 丢弃当前手牌（进弃牌堆）。
        var hand = combatPiles.Hand;
        var discarded = 0;
        foreach (var card in hand.Cards.ToList())
        {
            var result = await CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Bottom, null, skipVisuals: false);
            if (result.success)
            {
                discarded++;
            }
        }

        Core.Presentation.UI.CombatUiRefresh.RefreshAll(player);

        // ③ 间隙里的实例 → 手牌（真实实例，搬过去就能打）。
        var moved = 0;
        foreach (var card in copies)
        {
            // 先从**存储**牌堆摘下（静默：存储是 Headless，没有可视流程要照顾）；
            // 显示层那份镜像稍后由 GapView.SyncAsync 统一撤掉（走 CardPileCmd ⇒ 卡面当场消失）。
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

                // 虚无（Ethereal）：2026-10-14 定档**副本带虚无**（默认 on；设置页 · M2 仍可关）。
                if (Ethereal)
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

                Const.Logger.Warn($"[{Const.ModId}] [M2] {card.Id.Entry} 搬回手牌失败（success=false）→ 放回间隙");
            }
            catch (Exception ex)
            {
                Const.Logger.Error(
                    $"[{Const.ModId}] [M2] {card.Id.Entry} 搬回手牌抛异常（已放回间隙）："
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
                Const.Logger.Warn($"[{Const.ModId}] [M2] {card.Id.Entry} 回填战斗登记失败：{ex.Message}");
            }
        }

        Core.Presentation.UI.CombatUiRefresh.RefreshAll(player);

        // 存储搬空了 ⇒ 同步显示层（撤掉那些克隆）。
        await GapView.SyncAsync(player);
        gap.InvokeContentsChanged();
        Core.Presentation.UI.CombatUiRefresh.RefreshAll(player);

        // ④ 清除"间隙"记录（用户 2026-10-11 的口径）：激活之后间隙必须归空 ——
        //    副本是真实实例，被搬走即离开间隙，所以"记录"这里就是牌堆内容本身。
        //    正常路径到这里已经空了；若还有残留（搬回失败的那几张），如实打出来，不假装成功。
        var leftover = gap.Cards.Count;
        if (leftover > 0)
        {
            Const.Logger.Warn($"[{Const.ModId}] [M2] 激活后间隙仍有 {leftover} 张（搬回手牌失败，已留在间隙里）");
        }

        // 闸门记账：走到这里 = 代价已付、牌也搬过 ⇒ **这一次激活确实发生了**（哪怕个别牌没搬回），
        // 所以本回合额度占用与"上次动作 = 回归"都按事实记下。
        GapTurnLimit.Mark(player, GapTurnLimit.GapAction.Recall);

        var text = $"激活：花 {cost} 沙砾，弃掉手牌 {discarded} 张，间隙 {copies.Count} 张 → 手牌 {moved} 张"
                   + $"，间隙清空（剩 {leftover}）";
        Const.Logger.Info($"[{Const.ModId}] [M2] {text} ｜ 现在 {Describe(player)}");
        return new GapResult(moved > 0, text);
    }

    /// <summary>清空间隙（调试用：把里面的实例直接摘掉，不回手牌）。</summary>
    public static GapResult Clear(Player player)
    {
        var gap = Zone(player);
        if (gap is null)
        {
            return GapResult.Fail("M2 未启用");
        }

        var count = gap.Cards.Count;
        foreach (var card in gap.Cards.ToList())
        {
            gap.RemoveInternal(card, true);
        }

        gap.InvokeContentsChanged();
        MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(GapView.SyncAsync(Players_FirstOrNull()));
        Core.Presentation.UI.CombatUiRefresh.RefreshAll(Players_FirstOrNull());
        return GapResult.Success($"间隙已清空（原有 {count} 张）");
    }

    /// <summary>清空这条调试路径没有玩家参数，从当前战斗里找一个（找不到就返回 null）。</summary>
    private static MegaCrit.Sts2.Core.Entities.Players.Player? Players_FirstOrNull() =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance.DebugOnlyGetState()?.Players.FirstOrDefault();

}
