using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.FrozenForm;

/// <summary>
/// 「冻结形态」的<b>记录存储</b>：一张 <c>RunPersistent + Headless</c> 的牌堆。
/// </summary>
/// <remarks>
/// <para>
/// ① 为什么用牌堆：记录要跨战斗、随存档走（用户 2026-10-18 口径"存储做成 run 持久隐藏牌堆"）。
/// <c>Headless</c> ⇒ 不出现在顶栏、不占界面；真实例躺在玩家自己的 <c>Piles</c> 里。
/// </para>
/// <para>
/// ② 记录无条数上限；回放（下一次战斗的第一个玩家回合开始）会把整堆取空 ⇒ 不跨场累积。
/// </para>
/// <para>
/// ③ 存储里的卡是<b>克隆</b>（战斗副本，<c>DeckVersion</c> 已清空），所以不会反向影响母本 / 孪生牌组。
/// </para>
/// </remarks>
internal static class FrozenFormStore
{
    private static PileType? _zonePileType;
    private static string _zoneId = "-";

    /// <summary>牌堆是否已注册（注册发生在 <c>MechanismBootstrap</c>，正常跑局里恒为 true）。</summary>
    public static bool IsReady => _zonePileType is not null;

    /// <summary>牌堆完整注册 id（<c>STS2_AEONGLASS_CARDPILE_FROZEN_FORM_STORE</c>）。</summary>
    public static string ZoneId => _zoneId;

    /// <summary>由 <see cref="FrozenFormModule" /> 在注册牌堆后调用。</summary>
    public static void Configure(ModCardPileDefinition definition)
    {
        _zonePileType = definition.PileType;
        _zoneId = definition.Id;

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.FrozenForm} 记录牌堆已注册：{definition.Id}"
            + $"（Headless+RunPersistent，scope={definition.Scope}）");
    }

    /// <summary>取当前玩家的记录牌堆；未注册时返回 null。</summary>
    public static CardPile? Zone(Player player) =>
        _zonePileType is { } type ? CardPile.Get(type, player) : null;

    /// <summary>记录牌堆里现在有几张。</summary>
    public static int CountOf(Player player) => Zone(player)?.Cards.Count ?? 0;

    /// <summary>把一张克隆放进记录堆（静默；Headless 堆没有 UI 需要刷新）。</summary>
    public static bool Add(Player player, CardModel card)
    {
        var zone = Zone(player);
        if (zone is null)
        {
            return false;
        }

        zone.AddInternal(card, -1, true);
        return true;
    }

    /// <summary>把整堆取空并返回（回放用；调用方负责逐张打出）。</summary>
    public static List<CardModel> TakeAll(Player player)
    {
        var zone = Zone(player);
        if (zone is null)
        {
            return [];
        }

        var cards = zone.Cards.ToList();
        foreach (var card in cards)
        {
            zone.RemoveInternal(card, true);
        }

        return cards;
    }

    /// <summary>一行状态（日志用）。</summary>
    public static string Describe(Player? player)
    {
        if (player is null)
        {
            return "冻结形态：无玩家";
        }

        var zone = Zone(player);
        if (zone is null)
        {
            return "冻结形态：记录牌堆不可用";
        }

        if (zone.Cards.Count == 0)
        {
            return "冻结形态：无记录";
        }

        return "冻结形态：" + string.Join(", ", zone.Cards.Select(card => card.Id.Entry));
    }
}
