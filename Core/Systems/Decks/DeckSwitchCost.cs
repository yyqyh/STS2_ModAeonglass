using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>切换卡组的代价来源。</summary>
internal enum DeckSwitchCostSource
{
    /// <summary>免费（测试期默认）。</summary>
    None = 0,

    /// <summary>沙砾（M1）—— "再次动用时间"的第一种解释。</summary>
    Sand = 1,

    /// <summary>能量（战斗内当场付）。</summary>
    Energy = 2,
}

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

/// <summary>
/// M6「切换卡组」的<b>唯一扣费入口</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>纪律</b>：任何切换路径（调试按键 / 将来的卡牌 / 遗物）都必须走 <see cref="PayAsync" />，<b>不要</b>各自扣费。
/// 于是"定价"这件事永远只有一个地方要改：本文件 + 设置页那组参数。
/// </para>
/// <para>
/// <b>为什么必须定价（写进代码，别删）</b>：放开战斗内热切换后，
/// ① 切换太便宜 ⇒ 两副牌退化成"一副大牌组"，而且 M1 的翻转几乎等于每回合回满资源；
/// ② 切换太贵 ⇒ M1 的日常价值会被换组频率卡死。
/// 现在的默认值刻意是"免费 + 无冷却"（<b>只为测试</b>），正式定价落在阶段 ④。
/// </para>
/// <para>
/// ⚠️ <b>自动轮换不收费</b>：那是设计节奏（每场战斗自然发生），不是玩家主动操作 ——
/// 收费只针对"主动切换 / 热切换"。
/// </para>
    /// <para>
    /// ⚠️ <b>「消耗空隙」这一档已在 Step 1 删除</b>：花空隙会破坏 M1 的硬不变式
    /// （<c>沙砾 + 空隙 = 容量</c>，由"支付沙砾 → 生成等量空隙"维持），是计划里点名的"高风险 1"。
    /// 现在可选的代价来源只有：免费 / 沙砾 / 能量。
    /// </para>
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
            var haveSand = AeonglassSecondaryResources.Sand is null
                ? 0
                : SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId);
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

        // M1 关掉时沙砾 / 空隙没有注册：这时"用沙砾付账"是不可能的，直接失败并说明原因，
        // 不要在下面碰到 AeonglassSecondaryResources.SandId（那个 getter 会 NRE）。
        if (cost.Source is DeckSwitchCostSource.Sand && AeonglassSecondaryResources.Sand is null)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 代价来源是 {SourceName(cost.Source)}，但 M1 没启用 ⇒ 无法支付");
            return false;
        }

        try
        {
            // 附加费先扣（它不受"代价来源"的档位影响：热切换永远是沙砾）。
            if (extraSand > 0)
            {
                if (!await SecondaryResourceCmd.Spend(
                        player, AeonglassSecondaryResources.SandId, extraSand))
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
                            player, AeonglassSecondaryResources.SandId, cost.Amount))
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
        DeckSwitchCostSource.Sand when AeonglassSecondaryResources.Sand is not null =>
            SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId),
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
