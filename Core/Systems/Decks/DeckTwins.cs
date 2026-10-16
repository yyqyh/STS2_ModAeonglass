using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Content.Characters;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>两副基础牌的<b>孪生实例</b>：链接关系、事件订阅，以及"改一份、另一份跟着变"的镜像。</summary>
/// <remarks>
/// ① 职责：<c>Link</c>/<c>Unlink</c>/<c>TwinOf</c>/<c>IsLinked</c>/<c>RegisterPileWatch</c>/<c>ReleaseAll</c>/<c>Describe</c>；
///    各条镜像动作在 <c>DeckTwinMirrors</c>。
/// ② 挂点与时机（2026-10-16 起**事件驱动**，不再打 Harmony 补丁）：
///    卡级 —— <c>Link</c> 时订阅 <c>CardModel.Upgraded</c> / <c>CardModel.EnchantmentChanged</c>，<c>Unlink</c> 退订；
///    移牌 —— <c>DeckSeeding</c> 首次播种后（每场战斗开始再兜一次）对 <c>player.Deck</c> 与第二副牌堆订阅 <c>CardPile.CardRemoved</c>；
///    换 run —— <c>DeckModule</c> 的 <c>RunEndedEvent</c> 调 <c>ReleaseAll</c> 统一退订。
/// ③ 依赖：<c>CardModel</c> 的公开事件（本体在 <c>UpgradeInternal</c>/<c>DowngradeInternal</c> 末尾发 <c>Upgraded</c>、
///    在 <c>EnchantInternal</c>/<c>ClearEnchantmentInternal</c> 末尾发 <c>EnchantmentChanged</c>）、<c>CardPile.CardRemoved</c>、<c>DeckPiles</c>。
/// ④ ⚠️ 静默移牌（<c>RemoveInternal(card, silent: true)</c>）**不发事件** ⇒ 换副时的静默搬运不会再被误判成"删牌"
///    （旧补丁不看 <c>silent</c>，会在每次换副时把配对拆掉 —— 这条隐患是改事件驱动顺带修掉的）。
///    订阅与配对必须**一一对应**：漏退订会留下"幽灵镜像"，<c>Describe</c> 里的自检就是为它准备的。
///    两副共用同一批基础牌、但<b>不能是同一个对象</b>：差分判定看 <c>DeckVersion</c> 指向的母本躺在哪一副，同一对象会让判定失效。
/// </remarks>
internal static class DeckTwins
{
    /// <summary>孪生对照表（双向：a→b 与 b→a 都在里面；按引用比较）。</summary>
    private static readonly Dictionary<CardModel, CardModel> Pairs = new(ReferenceEqualityComparer.Instance);

    /// <summary>每张已配对卡的事件句柄（退订用；应与 <see cref="Pairs" /> 一一对应）。</summary>
    private static readonly Dictionary<CardModel, (Action Upgraded, Action EnchantmentChanged)> CardHandlers =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>已盯住的牌堆（移牌镜像用；按引用去重，重复注册是幂等的）。</summary>
    private static readonly Dictionary<CardPile, Action<CardModel>> PileHandlers =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>镜像进行中（防止"镜像 → 触发事件 → 再镜像回去"的回环）。</summary>
    internal static bool Mirroring;

    /// <summary>配一对孪生（双向登记），并订阅两张牌的卡级事件。</summary>
    public static void Link(CardModel a, CardModel b)
    {
        Pairs[a] = b;
        Pairs[b] = a;
        SubscribeCard(a);
        SubscribeCard(b);
    }

    /// <summary>这张牌的孪生（没配对返回 null）。</summary>
    public static CardModel? TwinOf(CardModel card) => Pairs.TryGetValue(card, out var twin) ? twin : null;

    /// <summary>这张牌有没有孪生。</summary>
    public static bool IsLinked(CardModel card) => Pairs.ContainsKey(card);

    /// <summary>拆掉一对孪生（删牌 / 变换 / 镜像删除时用）：**先退订**再删配对。</summary>
    public static void Unlink(CardModel card)
    {
        if (!Pairs.Remove(card, out var twin))
        {
            return;
        }

        Pairs.Remove(twin);
        UnsubscribeCard(card);
        UnsubscribeCard(twin);
    }

    /// <summary>清空并退订全部（换 run / 调试用）。</summary>
    public static void Clear() => ReleaseAll();

    /// <summary>
    /// 清掉"不属于这个玩家 / 这一局"的残留配对（**惰性自愈**）。
    /// </summary>
    /// <remarks>
    /// 为什么需要它：`RunEndedEvent` 并不覆盖所有结束路径（2026-10-16 实测：主菜单的
    /// "放弃游戏"不会发它）⇒ 上一局的配对会残留在表里并继续持有旧卡对象。这里在**播种前**按
    /// `card.Owner` 归属过滤一遍，漏释放也能在下一次播种时清零。
    /// </remarks>
    /// <returns>清掉的配对数。</returns>
    public static int PruneForeignPairs(Player player)
    {
        var pruned = 0;
        foreach (var card in Pairs.Keys.ToList())
        {
            if (ReferenceEquals(card.Owner, player))
            {
                continue;
            }

            var wasPaired = Pairs.ContainsKey(card);
            Unlink(card);
            if (wasPaired)
            {
                pruned++;
            }
        }

        if (pruned > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 清理残留配对 {pruned} 对（上一局的孪生未释放）"
                + $"｜{Describe()}");
        }

        return pruned;
    }

    /// <summary>换 run 时统一释放：退掉卡级与牌堆级全部句柄，再清空配对。</summary>
    public static void ReleaseAll()
    {
        foreach (var (card, handlers) in CardHandlers)
        {
            card.Upgraded -= handlers.Upgraded;
            card.EnchantmentChanged -= handlers.EnchantmentChanged;
        }

        CardHandlers.Clear();

        foreach (var (pile, handler) in PileHandlers)
        {
            pile.CardRemoved -= handler;
        }

        PileHandlers.Clear();
        Pairs.Clear();
    }

    /// <summary>
    /// 盯住一副牌堆的"离开"事件（移牌镜像的入口）。重复调用同一副牌堆是幂等的。
    /// </summary>
    /// <remarks>
    /// 只在<b>非静默</b>移牌时触发 —— 本体所有标准删牌路径都走 <c>CardPileCmd.RemoveFromDeck</c> ⇒
    /// <c>RemoveFromCurrentPile()</c>（<c>silent: false</c>）；我们自己的换副搬运走静默 ⇒ 不会误伤。
    /// </remarks>
    public static void RegisterPileWatch(Player player, CardPile? pile)
    {
        // 角色门：别人的局里不盯堆（配对表本来就是空的，这里连订阅都不建）。
        if (pile is null || player.Character is not Aeonglass || PileHandlers.ContainsKey(pile))
        {
            return;
        }

        void Handler(CardModel card) =>
            DeckTwinMirrors.Safe(
                nameof(CardPile.CardRemoved),
                () => DeckTwinMirrors.MirrorRemoval(pile, card));

        pile.CardRemoved += Handler;
        PileHandlers[pile] = Handler;

        // 观测：盯堆是"移牌镜像"的开关（每局就 2 条）—— 归「详细诊断日志」开关。
        if (AeonglassSettingStore.DebugVerboseLogs)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 盯堆 +1（type={pile.Type}）｜当前 {PileHandlers.Count} 副");
        }
    }

    /// <summary>把该玩家的两副牌堆都盯上（哪一副还没建出来就跳过，下一次调用会补上）。</summary>
    public static void EnsurePileWatch(Player player)
    {
        RegisterPileWatch(player, player.Deck);
        RegisterPileWatch(player, DeckPiles.InactivePileOf(player));
    }

    /// <summary>配了对的孪生有几对。</summary>
    public static int Count => Pairs.Count / 2;

    /// <summary>订阅自检：已订阅的卡数（应恒等于配对表条目数）。</summary>
    public static int SubscribedCards => CardHandlers.Count;

    /// <summary>一行状态（面板 / 日志用）；顺带做一次"配对表 ↔ 订阅表"自检，不一致就记 Warn。</summary>
    public static string Describe()
    {
        var audit = Pairs.Count == CardHandlers.Count
            ? "订阅自检 OK"
            : $"订阅自检 ⚠ 不一致（配对表 {Pairs.Count} 条 / 卡级订阅 {CardHandlers.Count} 条，检查 Link/Unlink 是否成对）";

        if (Pairs.Count != CardHandlers.Count)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Decks} 孪生{audit}");
        }

        return Pairs.Count == 0
            ? $"孪生：还没有配对的牌｜{audit}｜盯堆 {PileHandlers.Count}"
            : $"孪生：{Count} 对（例：{Pairs.First().Key.Id.Entry} ↔ {Pairs.First().Value.Id.Entry}）"
              + $"｜{audit}｜盯堆 {PileHandlers.Count}";
    }

    /// <summary>两个孪生现在是不是分处两副（正常应为"分处两副"）。</summary>
    internal static string DescribePlacement(CardModel a, CardModel b)
        => ReferenceEquals(a.Pile, b.Pile) ? "⚠ 同一牌堆（异常）" : "分处两副 ✔";

    // ============================ 附魔镜像（P3-3 的第二条路）============================
    // 附魔不走 `UpgradeInternal`，它有自己的单点：`CardModel.EnchantInternal(...)` / `ClearEnchantmentInternal()`
    // （本体的 `CardCmd.Enchant<T>` / `ClearEnchantment` 都走它们），两者末尾都发 `EnchantmentChanged`。

    // ============================ 删除 / 变换镜像（P3-3 的第三条路）============================
    // 本体所有"把一张牌从牌堆里拿掉"都走 `CardPile.RemoveInternal(card, silent)`（商店删牌、事件删牌、
    // 变换的前半"删一张"、清空牌堆…）。我们改盯 `CardPile.CardRemoved` —— 等价，且不看内部方法名。
    // 只对**牌组**这一层的离开做镜像 —— 手牌 / 抽牌堆的走动不该带走孪生。

    /// <summary>这个牌堆是不是"牌组"那一层（生效副 = <c>player.Deck</c>，另一副 = 模组牌堆）。</summary>
    internal static bool IsDeckPile(CardPile pile, Player player)
        => pile.Type == PileType.Deck || ReferenceEquals(pile, DeckPiles.InactivePileOf(player));

    /// <summary>订阅一张牌的卡级事件（同一张牌重复订阅会被忽略）。</summary>
    private static void SubscribeCard(CardModel card)
    {
        if (CardHandlers.ContainsKey(card))
        {
            return;
        }

        void Upgraded() =>
            DeckTwinMirrors.Safe(nameof(CardModel.Upgraded), () => DeckTwinMirrors.OnUpgradeChanged(card));

        void Enchantment() =>
            DeckTwinMirrors.Safe(
                nameof(CardModel.EnchantmentChanged), () => DeckTwinMirrors.OnEnchantmentChanged(card));

        card.Upgraded += Upgraded;
        card.EnchantmentChanged += Enchantment;
        CardHandlers[card] = (Upgraded, Enchantment);
    }

    /// <summary>退订一张牌的卡级事件。</summary>
    private static void UnsubscribeCard(CardModel card)
    {
        if (!CardHandlers.Remove(card, out var handlers))
        {
            return;
        }

        card.Upgraded -= handlers.Upgraded;
        card.EnchantmentChanged -= handlers.EnchantmentChanged;
    }
}
