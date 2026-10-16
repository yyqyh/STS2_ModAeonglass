using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 双卡组的<b>播种与孪生连接</b>：非生效副首次初始化 + 与 A 副同名基础牌配对。
/// </summary>
/// <remarks>
/// ① 职责：<c>EnsureInitialized</c>（只播种一次）+ <c>LinkSeededTwins</c>（按顺序配孪生）。
/// ② 调用时机：战斗开始 / 切副前（<c>DeckSwitchService</c> 与 run 钩子调）。
/// ③ 依赖：<c>DeckPiles</c>（堆）+ <c>DeckBSeed</c>（初始内容）+ <c>DeckTwins</c>（配对）。
/// ④ ⚠️ "只播种一次"由 <c>DeckState.DecksInitialized</c> 把关，重复播种会造成双份母本。
/// </remarks>
internal static class DeckSeeding
{

    /// <summary>
    /// 保证"非生效那一副"有内容（只在第一次做，靠遗物上的 <c>[SavedProperty]</c> 记）。
    /// </summary>
    public static void EnsureInitialized(Player player)
    {
        if (!DeckModule.Enabled || !DeckPiles.IsReady || DeckState.DecksInitialized(player))
        {
            return;
        }

        // 角色门（2026-10-15）：不是永世沙漏的玩家一步都不跑 —— 不播种、不配对、不写状态。
        // ⚠️ 实测漏过：静默猎手局里照常播种 10 张我们的牌并刷了一整屏日志（别人的局里我们的机制不该动）。
        if (player.Character is not Aeonglass)
        {
            return;
        }

        // 只有"A 生效"时才轮到播种 B：如果当前生效的是 B，说明这局已经切过卡组
        // （模组牌堆里躺着的是真 A），此时把测试牌倒进去等于污染玩家的牌组。
        if (DeckPiles.ActiveIndexOf(player) != 0)
        {
            DeckState.MarkInitialized(player);
            return;
        }

        var pile = DeckPiles.InactivePileOf(player);
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
        // 上一局的配对可能没被释放（RunEndedEvent 不覆盖"主菜单 → 放弃游戏"）⇒ 播种前先清一遍残留。
        DeckTwins.PruneForeignPairs(player);
        // 移牌镜像的入口：盯住两副牌堆的"离开"事件（不再打 CardPile.RemoveInternal 补丁）。
        // ⚠️ 放在配对**之前**：这样"孪生配对完成…盯堆"那行读到的才是真实值（上一版恒读到注册前的旧值）。
        DeckTwins.EnsurePileWatch(player);
        LinkSeededTwins(player, seededCards);
        DeckState.MarkInitialized(player);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Decks} 第二副牌组已播种：{DeckBSeed.Describe()}"
            + $"（实际入堆 {seeded} 张）｜{DeckPiles.DescribeActive(player)}");
    }

    /// <summary>
    /// 把刚播种进 B 的牌与 A 里**同名的牌按顺序一对一**配成孪生（P3-2：同一批基础牌，两副各一份实例）。
    /// </summary>
    /// <remarks>
    /// 配对只做一次（播种也只在第一次做）。配不上的（例如 A 里没有同名牌）就留空，
    /// 之后"改一张牌"时它自然没有孪生可镜像 —— 不会误伤。
    /// </remarks>
    internal static void LinkSeededTwins(Player player, IReadOnlyList<CardModel> seededCards)
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

    /// <summary>
    /// 读档继续（<c>RunLoadedEvent</c>）时把孪生**重连**一次。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 为什么需要：配对表是**运行时**表、不随存档走；而 <c>DecksInitialized</c> 存在遗物上、读档回来是 true
    /// ⇒ 播种会被幂等闸挡住，于是读档后镜像会静默失效。这里按"非生效副的牌 × 生效副的同名牌"重配一次。
    /// </para>
    /// <para>幂等：<c>LinkSeededTwins</c> 只挑"还没配对"的生效副牌做候选，已配对的不会重复连。</para>
    /// </remarks>
    public static void RelinkAfterLoad(Player player)
    {
        if (!DeckModule.Enabled || !DeckPiles.IsReady || player.Character is not Aeonglass)
        {
            return;
        }

        var pile = DeckPiles.InactivePileOf(player);
        if (pile is null || pile.Cards.Count == 0)
        {
            return;
        }

        DeckTwins.EnsurePileWatch(player);
        LinkSeededTwins(player, pile.Cards.ToList());
    }
}
