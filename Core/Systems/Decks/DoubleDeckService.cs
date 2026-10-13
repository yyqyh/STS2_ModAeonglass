using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>切换结果（结构化，方便 UI 原样显示）。</summary>
internal readonly record struct DeckSwitchOutcome(bool Ok, bool Switched, string Message)
{
    public static DeckSwitchOutcome Fail(string message) => new(false, false, message);

    public static DeckSwitchOutcome Done(string message) => new(true, true, message);
}

/// <summary>
/// M6「双卡组」的<b>内部接口单点</b>：所有切换（自动轮换 / 主动切换 / 战斗内热切换）都从这里走。
/// </summary>
/// <remarks>
/// <para>
/// <b>存储 = 活实例常驻</b>（2026-10-11 已拍定）："生效中的那副"永远真实地在 <c>player.Deck</c> 里，
/// 另一副作为**真母本实例**躺在 <c>deck_b</c> 这个 <c>RunPersistent</c> 牌堆里（照 M7 搁浅的形状）。
/// 切换 = 静默 <c>RemoveInternal</c>/<c>AddInternal</c> **搬运母本** ——
/// 升级 / 附魔 / 私有状态 / RitsuLib 的 <c>SecondaryCosts</c> 全都在实例上，会话内零丢失。
/// </para>
/// <para>
/// ⚠️ <b>存档仍然要落盘成 <c>SerializableCard</c></b>（唯一格式）⇒ **读档那一次**仍会丢
/// "未标 <c>[SavedProperty]</c> 的私有状态"与 <c>SecondaryCosts</c>。应对：
/// ① 我们自己的卡**约定"要长期存活的字段必须标 <c>[SavedProperty]</c>"**；
/// ② 读档后对带 <c>SecondaryCosts</c> 的卡补挂费用（复用 M2 的 <c>GapCopyRestore</c> 那套）。
/// </para>
/// <para>
/// <b>为什么 <c>ActiveDeckProvider.DeckOf(player)</c> 仍然是 <c>player.Deck</c></b>：
/// 因为这里是"**内容互换**"语义 —— 卡组层的"生效那一副"就放在 <c>player.Deck</c> 里，
/// 于是 M7 的归队（回到"当时的当前卡组"）与"新获得的牌落哪副"天然跟着走，不需要改任何调用点。
/// </para>
/// <para>
/// ⚠️ 将来若要支持多人：这个入口必须换成 RitsuLib 的**同步动作**（右键或等价），
/// 否则单端改卡组会让校验和分化、直接终止 run。
/// </para>
/// </remarks>
internal static class DoubleDeckService
{
    private static PileType? _pileType;

    /// <summary>第二副牌组的牌堆是否已注册（M6 关掉时是 false）。</summary>
    public static bool IsReady => _pileType is not null;

    /// <summary>由 <c>DecksModule.Register()</c> 在注册牌堆后调用。</summary>
    public static void Configure(ModCardPileDefinition definition)
    {
        _pileType = definition.PileType;
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Decks} 第二副牌组牌堆就绪：{definition.Id}"
            + $"（type=0x{(int)definition.PileType:X8}，scope={definition.Scope}）");
    }

    // ============================ 查询 ============================

    /// <summary>非生效那一副所在的牌堆（**先解析**：<c>Player.Piles</c> 只含已创建的 run 持久牌堆）。</summary>
    public static CardPile? InactivePileOf(Player player) =>
        _pileType is { } type ? CardPile.Get(type, player) : null;

    /// <summary>当前生效的卡组索引（0 = A，1 = B）。M6 关掉时恒 0。</summary>
    public static int ActiveIndexOf(Player player) => DecksModule.Enabled ? DeckState.ActiveIndex(player) : 0;

    /// <summary>
    /// 拿到指定卡组所在的牌堆。
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>不能写成 <c>index == 1 ? 模组牌堆 : player.Deck</c></b>：那只有"A 生效"时才成立。
    /// 切换是"内容互换"，B 生效时 A 才是躺在模组牌堆里的那一副。
    /// 正确判据只有一个：**跟当前生效索引相同的那一副在 <c>player.Deck</c>，另一副在模组牌堆里**。
    /// （2026-10-11 实测踩到：写反了会让日志/面板把"另一副"显示成当前那副的张数。）
    /// </remarks>
    public static CardPile? PileOfIndex(Player player, int index) =>
        index == ActiveIndexOf(player) ? player.Deck : InactivePileOf(player);

    /// <summary>指定卡组的张数。</summary>
    public static int CountOf(Player player, int index) => PileOfIndex(player, index)?.Cards.Count ?? 0;

    /// <summary>卡组显示名。</summary>
    public static string NameOf(int index) => index == 1 ? "B" : "A";

    /// <summary>
    /// <b>差分设计的判定单点</b>（Step 4 · P3）：这张卡属于哪一副（0 = A，1 = B）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判据 = **母本所在的容器**（<c>CardModel.DeckVersion</c>）：本体给"战斗内克隆"打上的
    /// <c>DeckVersion</c> 指向它在牌组里的母本，而母本此刻要么在 <c>player.Deck</c>（生效副）、
    /// 要么在 <c>deck_b</c> 牌堆（非生效副）。
    /// </para>
    /// <para>
    /// <c>DeckVersion == null</c> 的卡（例如 M2 的间隙副本、临时生成牌）**回退到"当前激活副"** ——
    /// 这样"间隙里可以跨副存牌"这条口径不会被差分判定破坏。
    /// </para>
    /// <para>Step 8 的卡表会用这个接口做"同一张卡在 A/B 下表现不同"。</para>
    /// </remarks>
    public static int DeckIndexOf(CardModel card, Player player)
    {
        var active = ActiveIndexOf(player);
        if (card.DeckVersion is not { } master)
        {
            return active;
        }

        if (master.Pile is null)
        {
            return active;
        }

        return master.Pile.Type == player.Deck.Type ? active : 1 - active;
    }

    /// <summary>"当前这副"一行。</summary>
    public static string DescribeActive(Player player) =>
        $"当前牌组 {NameOf(ActiveIndexOf(player))}（{player.Deck.Cards.Count} 张）";

    /// <summary>"下一场会用哪一副"一行（自动轮换开着时 = 另一副）。</summary>
    public static string DescribeNext(Player player)
    {
        if (!DecksModule.Enabled || !IsReady)
        {
            return DescribeActive(player);
        }

        var active = ActiveIndexOf(player);
        var next = AeonglassSettingStore.Current.DecksAutoSwitch ? 1 - active : active;
        return $"下一场：{NameOf(next)}（{CountOf(player, next)} 张）"
               + $"｜当前 {NameOf(active)}（{player.Deck.Cards.Count} 张）";
    }

    // ============================ 初始化 ============================

    /// <summary>
    /// 保证"非生效那一副"有内容（只在第一次做，靠遗物上的 <c>[SavedProperty]</c> 记）。
    /// </summary>
    public static void EnsureInitialized(Player player)
    {
        if (!DecksModule.Enabled || !IsReady || DeckState.DecksInitialized(player))
        {
            return;
        }

        // 只有"A 生效"时才轮到播种 B：如果当前生效的是 B，说明这局已经切过卡组
        // （模组牌堆里躺着的是真 A），此时把测试牌倒进去等于污染玩家的牌组。
        if (ActiveIndexOf(player) != 0)
        {
            DeckState.MarkInitialized(player);
            return;
        }

        var pile = InactivePileOf(player);
        if (pile is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Decks} 播种失败：第二副牌组牌堆不可用");
            return;
        }

        var seeded = 0;
        var seededCards = new List<CardModel>();
        foreach (var card in DeckBSeed.Create(player))
        {
            if (!pile.Cards.Contains(card))
            {
                pile.AddInternal(card, -1, true);
                seededCards.Add(card);
                seeded++;
            }
        }

        pile.InvokeContentsChanged();
        LinkSeededTwins(player, seededCards);
        DeckState.MarkInitialized(player);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Decks} 第二副牌组已播种：{DeckBSeed.Describe()}"
            + $"（实际入堆 {seeded} 张）｜{DescribeActive(player)}");
    }

    /// <summary>
    /// 把刚播种进 B 的牌与 A 里**同名的牌按顺序一对一**配成孪生（P3-2：同一批基础牌，两副各一份实例）。
    /// </summary>
    /// <remarks>
    /// 配对只做一次（播种也只在第一次做）。配不上的（例如 A 里没有同名牌）就留空，
    /// 之后"改一张牌"时它自然没有孪生可镜像 —— 不会误伤。
    /// </remarks>
    private static void LinkSeededTwins(Player player, IReadOnlyList<CardModel> seededCards)
    {
        if (seededCards.Count == 0)
        {
            return;
        }

        var candidates = player.Deck.Cards.Where(card => !DeckTwins.IsLinked(card)).ToList();
        // P3-4 守门：共用基础牌不该带"离开牌堆"类关键词（只警告，不阻断）。
        SharedBasicPolicy.Audit(candidates);

        var linked = 0;
        foreach (var twin in seededCards)
        {
            var index = candidates.FindIndex(card => card.Id.Entry == twin.Id.Entry);
            if (index < 0)
            {
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Decks} 孪生：{twin.Id.Entry} 在生效副里找不到同名的基础牌 ⇒ 不配对");
                continue;
            }

            DeckTwins.Link(candidates[index], twin);
            candidates.RemoveAt(index);
            linked++;
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Decks} 孪生配对完成：{linked} 对（共 {DeckTwins.Count} 对）"
            + $"｜{DeckTwins.Describe()}");
    }

    // ============================ 切换 ============================

    /// <summary>战斗胜利后的<b>自动轮换</b>（不收费；战斗已结束，所以不重建战斗牌堆）。</summary>
    public static async Task<DeckSwitchOutcome> AutoRotateAsync(Player player)
    {
        if (!DecksModule.Enabled || !IsReady)
        {
            return DeckSwitchOutcome.Fail("M6 未启用");
        }

        EnsureInitialized(player);
        return await SwitchAsync(player, DeckSwitchMode.Auto, null);
    }

    /// <summary>
    /// <b>主动切换</b>（含战斗内热切换）：过闸门 → 付代价 → 换内容 → 战斗中重建牌堆。
    /// </summary>
    public static async Task<DeckSwitchOutcome> ManualSwitchAsync(
        Player player,
        PlayerChoiceContext? choiceContext)
    {
        if (!DecksModule.Enabled)
        {
            return DeckSwitchOutcome.Fail("M6 未启用");
        }

        if (!IsReady)
        {
            return DeckSwitchOutcome.Fail("第二副牌组牌堆没注册（需要重启游戏）");
        }

        var setting = AeonglassSettingStore.Current;
        if (!setting.DecksManualSwitch)
        {
            return DeckSwitchOutcome.Fail("设置里关掉了「主动切换」");
        }

        EnsureInitialized(player);
        if (InactivePileOf(player) is null)
        {
            return DeckSwitchOutcome.Fail("第二副牌组牌堆不可用");
        }

        // 闸门 ①：每场战斗主动切换次数上限（0 = 不限，测试期默认）。
        var limit = Math.Clamp(setting.DecksSwitchesPerCombat, 0, 9);
        if (limit > 0 && DeckCombatLimit.UsedCountThisCombat(player) >= limit)
        {
            return DeckSwitchOutcome.Fail($"本场战斗主动切换已达上限（{limit} 次）");
        }

        // 闸门 ②：主动切换冷却（0 = 无冷却，测试期默认；正式候选"每 3 场 1 次"）。
        var cooldown = Math.Clamp(setting.DecksManualCooldownCombats, 0, 99);
        var since = DeckState.CombatsSinceManualSwitch(player);
        if (!AeonglassSettingStore.DebugUnlimited && cooldown > 0 && since < cooldown)
        {
            return DeckSwitchOutcome.Fail($"主动切换冷却中：还需 {cooldown - since} 场战斗");
        }

        // 闸门 ③：代价（**唯一扣费入口**）。
        // 热切换（战斗中）额外收"沙砾附加费"（Step 4）：把整副牌换掉并重铺这一场比场间换副更值钱。
        var hotNow = setting.DecksHotSwitch && CombatManager.Instance.IsInProgress;
        var extraSand = hotNow ? Math.Clamp(setting.DecksHotSwitchSandCost, 0, 9) : 0;

        if (!DeckSwitchCost.CanPay(player, out var reason, extraSand))
        {
            return DeckSwitchOutcome.Fail(reason);
        }

        if (!await DeckSwitchCost.PayAsync(player, extraSand))
        {
            return DeckSwitchOutcome.Fail("代价没付成功，已取消切换");
        }

        var outcome = await SwitchAsync(player, DeckSwitchMode.Manual, choiceContext);
        if (!outcome.Ok)
        {
            return outcome;
        }

        DeckCombatLimit.Mark(player);
        DeckState.NoteManualSwitch(player);
        return outcome;
    }

    /// <summary>切换模式（只影响日志与"要不要重建战斗牌堆"）。</summary>
    private enum DeckSwitchMode
    {
        Auto,
        Manual,
    }

    private static async Task<DeckSwitchOutcome> SwitchAsync(
        Player player,
        DeckSwitchMode mode,
        PlayerChoiceContext? choiceContext)
    {
        var from = ActiveIndexOf(player);
        var to = 1 - from;

        if (!SwapMasters(player))
        {
            return DeckSwitchOutcome.Fail("搬运卡组母本失败");
        }

        DeckState.SetActiveIndex(player, to);

        var hot = mode == DeckSwitchMode.Manual
                  && AeonglassSettingStore.Current.DecksHotSwitch
                  && CombatManager.Instance.IsInProgress;

        if (hot)
        {
            await RebuildCombatPilesAsync(player, choiceContext);
        }

        // 张数带牌组名，避免"A/B"与"主卡组/牌堆"两种读法混在一起看不清（实测反馈）。
        var text = $"{NameOf(from)} → {NameOf(to)}"
                   + $"（{(mode == DeckSwitchMode.Auto ? "自动轮换" : "主动切换")}"
                   + $"{(hot ? " + 战斗内热切换" : string.Empty)}，"
                   + $"现在生效 {NameOf(to)}={player.Deck.Cards.Count} 张，"
                   + $"另一副 {NameOf(from)}={CountOf(player, from)} 张）";

        // 交换会让"牌组"整副换人（数量也变）+ 战斗中热切换还会重铺五堆 ⇒ 立刻全刷 UI（用户 2026-10-12）。
        Core.Presentation.UI.CombatUiRefresh.RefreshAll(player);

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 切换卡组：{text}");
        return DeckSwitchOutcome.Done(text);
    }

    /// <summary>
    /// 静默搬运母本：把 <c>player.Deck</c> 与"非生效牌堆"的内容**互换**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 全走 <c>CardPile.RemoveInternal</c>/<c>AddInternal</c>（同步、静默）——
    /// 不用 <c>CardPileCmd.RemoveFromDeck</c>：那会为每张牌记地图历史、发移除钩子、播删除动画，
    /// 而"换一副牌"不是"删一堆牌"。也正因为没走那条路，母本的 <c>HasBeenRemovedFromState</c>
    /// 始终是 false，放回时不需要清标志（M7 那套标志处理这里用不上）。
    /// </para>
    /// <para>
    /// 两张母本全程都没离开 <c>RunState</c>（只是换了个牌堆），所以**不需要** <c>AddCard/RemoveCard</c>。
    /// </para>
    /// </remarks>
    private static bool SwapMasters(Player player)
    {
        var pile = InactivePileOf(player);
        if (pile is null)
        {
            return false;
        }

        var deck = player.Deck;
        var deckCards = deck.Cards.ToList();
        var pileCards = pile.Cards.ToList();

        // 先把两边都摘空，再互填 —— 避免"同一步里既读又写"造成张数错位。
        foreach (var card in deckCards)
        {
            if (deck.Cards.Contains(card))
            {
                deck.RemoveInternal(card, true);
            }
        }

        foreach (var card in pileCards)
        {
            if (pile.Cards.Contains(card))
            {
                pile.RemoveInternal(card, true);
            }
        }

        foreach (var card in pileCards)
        {
            if (!deck.Cards.Contains(card))
            {
                deck.AddInternal(card, -1, true);
            }
        }

        foreach (var card in deckCards)
        {
            if (!pile.Cards.Contains(card))
            {
                pile.AddInternal(card, -1, true);
            }
        }

        // 两边的 UI（顶栏牌组按钮、第二副牌堆按键）都要知道内容变了。
        deck.InvokeContentsChanged();
        pile.InvokeContentsChanged();
        return true;
    }

    /// <summary>
    /// 战斗内热切换：把本场战斗的牌堆按新卡组重铺一遍。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 照堕化拉撒路已实机验证过的路径：**只清 <c>PlayerCombatState.AllPiles</c>**
    /// （手牌 / 抽牌 / 弃牌 / 消耗 / 出牌区），<b>不动主卡组</b>；旧手牌是"**移出战斗**"
    /// （<c>CardPileCmd.RemoveFromCombat</c>），<b>不是弃牌、也不是消耗</b> —— 不触发任何弃牌 / 消耗类效果。
    /// </para>
    /// <para>
    /// 重建走本体的 <c>player.PopulateCombatState(rng, combatState)</c>（战斗开始时的同一条流程：
    /// 克隆卡组 → 进抽牌堆 → 洗牌），随后补抽一手（张数 = 换之前的手牌数，没有就按本体的 5 张）。
    /// </para>
    /// <para>
    /// ⚠️ M2 的间隙牌堆与 M6 的第二副牌组都是 <c>RunPersistent</c>，**不在** <c>AllPiles</c> 里
    /// ⇒ 热切换不会动它们（"A 组的牌可以躺在 B 的手牌里"这条口径因此天然成立）。
    /// </para>
    /// </remarks>
    private static async Task RebuildCombatPilesAsync(Player player, PlayerChoiceContext? choiceContext)
    {
        if (!CombatManager.Instance.IsInProgress
            || player.PlayerCombatState is not { } combatPiles
            || player.Creature.CombatState is not CombatState combatState)
        {
            return;
        }

        var handBefore = combatPiles.Hand.Cards.Count;

        // ① 本场战斗里现有的牌**全部移出战斗**（不是弃牌 / 消耗）。
        var combatCards = combatPiles.AllPiles.SelectMany(pile => pile.Cards).ToList();
        if (combatCards.Count > 0)
        {
            await CardPileCmd.RemoveFromCombat(combatCards, skipVisuals: false);
        }

        // ② 用新卡组重铺（克隆 → 进抽牌堆 → 洗牌）。
        player.PopulateCombatState(player.RunState.Rng.Shuffle, combatState);

        foreach (var pile in combatPiles.AllPiles)
        {
            pile.InvokeContentsChanged();
        }

        // ③ 补抽一手；没有出牌上下文就不抽，等下一次自然抽牌。
        if (choiceContext is null)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 热切换完成（没有出牌上下文，未补抽手牌）");
            return;
        }

        var drawCount = handBefore > 0 ? handBefore : CombatManager.baseHandDrawCount;
        var drawn = (await CardPileCmd.Draw(choiceContext, drawCount, player)).Count();
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 热切换完成，补抽 {drawn}/{drawCount} 张");
    }
}
