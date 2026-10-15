using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Persistence;

namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>
/// 搁浅的<b>存储</b>：显示牌堆本体、容量与读数。
/// </summary>
/// <remarks>
/// ① 职责：Configure/Zone/ZoneCount/ZoneCards/Describe。
/// ② 调用时机：模块注册、UI 与控制面板读取。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class StasisStore
{
    private static PileType? _zonePileType;
    private static string _zoneId = string.Empty;

    /// <summary>搁浅牌堆的完整 id（注册后才有值）。</summary>
    public static string ZoneId => _zoneId;

    /// <summary>牌堆是否已经注册好（搁浅 关掉时是 false，卡会退化成普通牌）。</summary>
    public static bool IsReady => _zonePileType is not null;

    /// <summary>由 <see cref="ExtraPileModules.Register" /> 在注册牌堆后调用。</summary>
    public static void Configure(ModCardPileDefinition definition)
    {
        _zonePileType = definition.PileType;
        _zoneId = definition.Id;

        Const.Logger.Info(
            $"[{Const.ModId}] [搁浅] 搁浅牌堆已注册：{definition.Id}"
            + $"（type=0x{(int)definition.PileType:X8}，scope={definition.Scope}）");
    }

    /// <summary>取搁浅牌堆；未注册或未解析时返回 null。</summary>
    public static CardPile? Zone(Player player) =>
        _zonePileType is { } type ? CardPile.Get(type, player) : null;

    /// <summary>搁浅里现在有几张。</summary>
    public static int ZoneCount(Player player) => Zone(player)?.Cards.Count ?? 0;

    /// <summary>搁浅里的牌（快照）。</summary>
    public static IReadOnlyList<CardModel> ZoneCards(Player player) => Zone(player)?.Cards ?? [];

    /// <summary>一行状态（日志 / 调试面板用）。</summary>
    public static string Describe(Player? player)
    {
        if (player is null)
        {
            return "无玩家";
        }

        var zone = Zone(player);
        if (zone is null)
        {
            return "搁浅牌堆不可用";
        }

        if (zone.Cards.Count == 0)
        {
            return "搁浅：空";
        }

        return "搁浅：" + string.Join(", ", zone.Cards.Select(card =>
            card is IStasisCard stasis ? $"{card.Id.Entry}(剩{stasis.CombatsLeft})" : card.Id.Entry));
    }

    /// <summary>牌堆名：模组牌堆打印注册 id，原版牌堆打印枚举名。</summary>
    internal static string PileName(PileType? type) =>
        type is not { } value
            ? "none"
            : ModCardPileRegistry.TryGetId(value, out var id)
                ? id
                : value.ToString();
}
