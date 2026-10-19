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

    /// <summary>
    /// <b>任意卡</b>的搁浅倒计时<b>侧表</b>（键 = 搁浅里的那张母本；值 = 还剩几次"战斗结束"）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 为什么需要：倒计时原来只存在 <see cref="IStasisCard.CombatsLeft" />（卡类型上的 <c>[SavedProperty]</c>），
    /// 而「停泊 / 倾囊」这类卡会把<b>任意</b>手牌弄进搁浅（打击、防御……它们不实现接口）⇒ 用这张 run 级侧表兜住。
    /// </para>
    /// <para>
    /// ⚠️ 侧表<b>不随存档走</b>：读档后若某张搁浅卡查不到倒计时，<c>StasisReturn.TickAfterCombatEnd</c>
    /// 会按 <see cref="ReArmCombats" /> 重新挂上并留一行 Warn。归队后残留的条目无害（下次入堆会被覆盖）。
    /// </para>
    /// </remarks>
    private static readonly Dictionary<CardModel, int> Countdowns = [];

    /// <summary>读档丢了倒计时时的兜底重挂场数（内部按 X + 1 存 ⇒ 2 = 再搁浅 1 场）。</summary>
    internal const int ReArmCombats = 2;

    /// <summary>挂倒计时：实现了 <see cref="IStasisCard" /> 的卡写它自己的 [SavedProperty]，其余走侧表。</summary>
    internal static void Arm(CardModel master, int combatsLeft)
    {
        if (master is IStasisCard stasis)
        {
            stasis.CombatsLeft = combatsLeft;
            return;
        }

        Countdowns[master] = combatsLeft;
    }

    /// <summary>取倒计时（接口优先，其次侧表）；都没有返回 false（读档丢失）。</summary>
    internal static bool TryGetCountdown(CardModel master, out int combatsLeft)
    {
        if (master is IStasisCard stasis)
        {
            combatsLeft = stasis.CombatsLeft;
            return true;
        }

        return Countdowns.TryGetValue(master, out combatsLeft);
    }

    /// <summary>写回倒计时（接口优先，其次侧表）。</summary>
    internal static void SetCountdown(CardModel master, int combatsLeft)
    {
        if (master is IStasisCard stasis)
        {
            stasis.CombatsLeft = combatsLeft;
            return;
        }

        Countdowns[master] = combatsLeft;
    }

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
            $"[{Const.ModId}] {Const.Prefix.Stasis} 搁浅牌堆已注册：{definition.Id}"
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
            TryGetCountdown(card, out var left) ? $"{card.Id.Entry}(剩{left})" : card.Id.Entry));
    }

    /// <summary>牌堆名：模组牌堆打印注册 id，原版牌堆打印枚举名。</summary>
    internal static string PileName(PileType? type) =>
        type is not { } value
            ? "none"
            : ModCardPileRegistry.TryGetId(value, out var id)
                ? id
                : value.ToString();
}
