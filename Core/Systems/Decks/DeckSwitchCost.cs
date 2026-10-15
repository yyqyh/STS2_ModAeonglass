using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>一组代价参数。</summary>
internal readonly record struct DeckSwitchCostConfig(DeckSwitchCostSource Source, int Amount)
{
    /// <summary>不用付钱。</summary>
    public bool IsFree => Source == DeckSwitchCostSource.None || Amount <= 0;

    /// <summary>给玩家 / 日志看的一行。</summary>
    public string Describe() => IsFree
        ? "免费"
        : Source switch
        {
            DeckSwitchCostSource.Sand => $"{Amount} 沙砾",
            DeckSwitchCostSource.Energy => $"{Amount} 能量",
            _ => "免费",
        };
}

/// <summary>双卡组「切换卡组」的<b>唯一扣费入口</b>。</summary>
/// <remarks>
/// ① 职责：<c>PayAsync</c> 按 <c>DeckSwitchCostConfig</c> 扣费；<c>DeckSwitchCostSource</c> 决定用哪种资源。
/// ② 挂点与时机：任何切换路径（调试按键 / 卡牌 / 遗物）在真正切换之前调。
/// ③ 依赖：<c>SandSpend</c>（沙砾档）、能量扣费、设置项。
/// ④ ⚠️ 纪律：必须走 <see cref="PayAsync" />，<b>不要</b>各自扣费 ⇒ 定价只有本文件 + 设置页一处要改（这段说明别删）。
/// 为什么必须定价：切换太便宜 ⇒ 两副牌退化成"一副大牌组"、翻转几乎等于每回合回满资源；太贵 ⇒ 时之沙的日常价值被换组频率卡死。
/// <b>自动轮换不收费</b>（设计节奏，不是玩家主动操作）；「消耗空隙」那一档已删除 —— 花空隙会破坏 <c>沙砾 + 空隙 = 容量</c> 的硬不变式。
/// </remarks>
internal static class DeckSwitchCost
{
    /// <summary>设置页里的字符串 → 枚举（非法值一律当免费）。</summary>
    public static DeckSwitchCostSource ParseSource(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "sand" => DeckSwitchCostSource.Sand,
        "energy" => DeckSwitchCostSource.Energy,
            // 历史值 "void"（消耗空隙）在 Step 1 被删除 ⇒ 落到免费，不会静默变成别的代价。
        _ => DeckSwitchCostSource.None,
    };

    /// <summary>当前代价（读设置，改完立刻生效）。</summary>
    public static DeckSwitchCostConfig Current
    {
        get
        {
            var setting = AeonglassSettingStore.Current;
            return new DeckSwitchCostConfig(
                ParseSource(setting.DecksCostSource),
                Math.Clamp(setting.DecksCostAmount, 0, 99));
        }
    }

    /// <summary>够不够付？不够时 <paramref name="reason" /> 是给玩家看的原因。</summary>
    /// <param name="extraSand">额外沙砾（热切换附加费；Step 4）。</param>
    public static bool CanPay(Player player, out string reason, int extraSand = 0)
    {
        var cost = Current;
        if (extraSand > 0)
        {
            var haveSand = SandResourceRegistry.Sand is null
                ? 0
                : SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId);
            if (haveSand < extraSand)
            {
                reason = $"热切换附加费不足：需要 {extraSand} 沙砾，现有 {haveSand}";
                return false;
            }
        }

        if (cost.IsFree)
        {
            reason = string.Empty;
            return true;
        }

        var have = Have(player, cost.Source);
        if (have >= cost.Amount)
        {
            reason = string.Empty;
            return true;
        }

        reason = $"代价不足：需要 {cost.Amount} {SourceName(cost.Source)}，现有 {have}";
        return false;
    }

    /// <summary><b>唯一扣费入口</b>。返回 false = 没付成功（调用方必须中止切换）。</summary>
    /// <param name="extraSand">额外沙砾（热切换附加费；Step 4）。</param>
    public static async Task<bool> PayAsync(Player player, int extraSand = 0)
    {
        var cost = Current;
        if (cost.IsFree && extraSand <= 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 切换代价：免费（当前设置）");
            return true;
        }

        // 时之沙 关掉时沙砾 / 空隙没有注册：这时"用沙砾付账"是不可能的，直接失败并说明原因，
        // 不要在下面碰到 SandResourceRegistry.SandId（那个 getter 会 NRE）。
        if (cost.Source is DeckSwitchCostSource.Sand && SandResourceRegistry.Sand is null)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 代价来源是 {SourceName(cost.Source)}，但 时之沙 没启用 ⇒ 无法支付");
            return false;
        }

        try
        {
            // 附加费先扣（它不受"代价来源"的档位影响：热切换永远是沙砾）。
            if (extraSand > 0)
            {
                if (!await SecondaryResourceCmd.Spend(
                        player, SandResourceRegistry.SandId, extraSand))
                {
                    return false;
                }

                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Decks} 热切换附加费已付：{extraSand} 沙砾");
            }

            if (cost.IsFree)
            {
                return true;
            }

            switch (cost.Source)
            {
                case DeckSwitchCostSource.Sand:
                    if (!await SecondaryResourceCmd.Spend(
                            player, SandResourceRegistry.SandId, cost.Amount))
                    {
                        return false;
                    }

                    break;

                case DeckSwitchCostSource.Energy:
                    var combat = player.PlayerCombatState;
                    if (combat is null || combat.Energy < cost.Amount)
                    {
                        return false;
                    }

                    combat.LoseEnergy(cost.Amount);
                    break;

                default:
                    return true;
            }
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Decks} 收取切换代价时抛异常：{ex.GetType().Name}: {ex.Message}");
            return false;
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 切换代价已付：{cost.Describe()}");
        return true;
    }

    private static int Have(Player player, DeckSwitchCostSource source) => source switch
    {
        DeckSwitchCostSource.Sand when SandResourceRegistry.Sand is not null =>
            SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId),
        DeckSwitchCostSource.Energy => player.PlayerCombatState?.Energy ?? 0,
        _ => 0,
    };

    private static string SourceName(DeckSwitchCostSource source) => source switch
    {
        DeckSwitchCostSource.Sand => "沙砾",
        DeckSwitchCostSource.Energy => "能量",
        _ => "？",
    };
}
