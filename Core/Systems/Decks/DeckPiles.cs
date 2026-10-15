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

/// <summary>双卡组的<b>内部接口单点（存储侧）</b>：堆访问、索引、命名与描述。</summary>
/// <remarks>
/// ① 职责：<c>Configure</c> / <c>PileOfIndex</c> / <c>CountOf</c> / <c>NameOf</c> / <c>DeckIndexOf</c> / <c>DescribeActive</c> / <c>DescribeNext</c>；切换流程在 <c>DeckSwitchService</c>。
/// ② 挂点与时机：所有切换（自动轮换 / 主动切换 / 战斗内热切换）与 UI 读数都从这里走。
/// ③ 依赖：<c>deck_b</c> 牌堆（<c>RunPersistent</c>）、<c>Player.Deck</c>、<c>ActiveDeckProvider</c>。
/// ④ ⚠️ <b>存储 = 活实例常驻</b>（2026-10-11 拍定）：生效那副真在 <c>player.Deck</c>，另一副作为真母本躺在 <c>deck_b</c>（照搁浅的形状）；
/// 切换 = 静默 <c>RemoveInternal</c>/<c>AddInternal</c> 搬运母本 ⇒ 升级 / 附魔 / 私有状态 / <c>SecondaryCosts</c> 会话内零丢失。
/// 存档仍要落盘成 <c>SerializableCard</c> ⇒ <b>读档那一次</b>仍会丢"未标 <c>[SavedProperty]</c> 的私有状态"与 <c>SecondaryCosts</c>：
/// 我们的卡约定"长期存活的字段必须标 <c>[SavedProperty]</c>"，读档后按 <c>GapCopyRestore</c> 那套补挂费用。
/// <c>ActiveDeckProvider.DeckOf</c> 仍是 <c>player.Deck</c>（"内容互换"语义 ⇒ 搁浅归队与"新牌落哪副"天然跟着走）。
/// 将来支持多人时这个入口必须换成 RitsuLib 的同步动作，否则单端改卡组会让校验和分化、终止 run。
/// </remarks>
internal static class DeckPiles
{
    private static PileType? _pileType;

    /// <summary>第二副牌组的牌堆是否已注册（双卡组 关掉时是 false）。</summary>
    public static bool IsReady => _pileType is not null;

    /// <summary>第二副牌组的牌堆类型（用于"认出顶栏上那颗副卡组按钮"；未注册时 null）。</summary>
    public static PileType? DeckBPileType => _pileType;

    /// <summary>由 <c>DeckModule.Register()</c> 在注册牌堆后调用。</summary>
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

    /// <summary>当前生效的卡组索引（0 = A，1 = B）。双卡组 关掉时恒 0。</summary>
    public static int ActiveIndexOf(Player player) => DeckModule.Enabled ? DeckState.ActiveIndex(player) : 0;

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
    /// 判据分三段（顺序即优先级）：
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <b>战斗内的克隆</b>：跟<b>母本所在的容器</b>走 —— 本体给克隆打上的
    ///     <c>CardModel.DeckVersion</c> 指向它在牌组里的母本，而母本此刻要么在
    ///     <c>player.Deck</c>（生效副）、要么在 <c>deck_b</c> 牌堆（非生效副）。
    ///   </item>
    ///   <item>
    ///     <b>母本自己</b>：看它**现在躺在哪个牌堆**。
    ///     ⚠️ 这一段是 2026-10-14 补的：本体<b>只在战斗克隆上</b>写 <c>DeckVersion</c>
    ///     （<c>Player.PopulateCombatState</c> 里克隆后赋值，克隆体 <c>AfterCloned</c> 里清空），
    ///     母本的它<b>恒为 null</b> —— 只走第 ① 段的话，战斗外打开牌组详情页，
    ///     两副的卡会一起落到"当前生效副"的配色/文案（用户实测报的就是这个）。
    ///   </item>
    ///   <item>
    ///     <b>实例上记着来源副</b>（时空裂隙副本 / 它的显示投影，见 <see cref="DeckOrigin" />）：
    ///     认这条记录 —— "间隙卡回到手牌保持原本差分"就是这个口径。
    ///   </item>
    ///   <item>
    ///     <b>其余</b>（奖励选卡 / 商店 / 战斗内生成 / 没记过来源的间隙副本）：回退到
    ///     <b>"当前激活副"</b> —— "选卡同当前卡组"就是这个口径，同时"间隙里可以跨副存牌"
    ///     这条也不会被差分判定破坏（归队也是回到当前生效副，与
    ///     <see cref="ActiveDeckProvider.DeckOf" /> 一致）。
    ///   </item>
    /// </list>
    /// <para>Step 8 的卡表会用这个接口做"同一张卡在 A/B 下表现不同"。</para>
    /// </remarks>
    public static int DeckIndexOf(CardModel card, Player player)
    {
        var active = ActiveIndexOf(player);

        // ① 归属容器就是两副牌组之一 ⇒ 按牌堆判：
        //    战斗内的克隆看母本（DeckVersion），战斗外的母本看它自己。
        //    ⚠️ 只认"两副牌组"这两个容器 —— 手牌 / 抽弃牌堆 / 间隙 / 搁浅 都不算"归属容器"。
        var holder = card.DeckVersion is { } master ? master.Pile : card.Pile;
        if (holder is { } pile && IsDeckPile(pile, player))
        {
            return IsActiveDeckPile(pile, player) ? active : 1 - active;
        }

        // ② 实例上记着"来自哪一副"（时空裂隙副本、以及它的显示投影）⇒ 认它。
        if (DeckOrigin.TryResolve(card, out var origin))
        {
            return (int)origin;
        }

        // ③ 母本明确落在别的模组牌堆里（搁浅 搁浅 / 间隙）—— 保留旧口径：算"非生效那一副"。
        if (card.DeckVersion is { Pile: not null })
        {
            return 1 - active;
        }

        // ④ 其余（奖励选卡 / 商店 / 战斗内生成 / 没记过来源的副本）：跟当前生效那一副。
        return active;
    }

    /// <summary>这个牌堆是不是"当前生效那一副"（<c>player.Deck</c>）。</summary>
    private static bool IsActiveDeckPile(CardPile pile, Player player) => pile.Type == player.Deck.Type;

    /// <summary>这个牌堆是不是"两副牌组之一"（<c>player.Deck</c> 或第二副那个 <c>RunPersistent</c> 牌堆）。</summary>
    private static bool IsDeckPile(CardPile pile, Player player) =>
        IsActiveDeckPile(pile, player) || (_pileType is { } deckB && pile.Type == deckB);

    /// <summary>"当前这副"一行。</summary>
    public static string DescribeActive(Player player) =>
        $"当前牌组 {NameOf(ActiveIndexOf(player))}（{player.Deck.Cards.Count} 张）";

    /// <summary>"下一场会用哪一副"一行（自动轮换开着时 = 另一副）。</summary>
    public static string DescribeNext(Player player)
    {
        if (!DeckModule.Enabled || !IsReady)
        {
            return DescribeActive(player);
        }

        var active = ActiveIndexOf(player);
        var next = AeonglassSettingStore.Current.DecksAutoSwitch ? 1 - active : active;
        return $"下一场：{NameOf(next)}（{CountOf(player, next)} 张）"
               + $"｜当前 {NameOf(active)}（{player.Deck.Cards.Count} 张）";
    }

    // ============================ 初始化 ============================

    // ============================ 切换 ============================

}
