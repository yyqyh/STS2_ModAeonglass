using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Presentation.UI;

namespace STS2_Aeonglass.Core.Systems.SandResource;

/// <summary>
/// M1「倒置」：把沙砾与空隙对调（<b>2 费</b>）。
/// </summary>
/// <remarks>
/// <para>
/// <b>唯一入口 = <see cref="TryInvert" /></b>（Step 1 收口）：沙砾计数器本体可点（点击即倒置），
/// 卡牌 / 遗物也只能调它。<b>不要再加第二个扣能量 + 交换的入口。</b>
/// </para>
/// <para>
/// <b>每回合次数上限已取消</b>（Step 1）：这条上限是早期"防滥用"的兜底，规则层 v1 定稿后去掉 ——
/// 现在只有 2 费这一个约束（花钱就能连着倒）。
/// 测试期的"解除所有次数限制"开关对它已无意义（因为这里不再计数）。
/// </para>
/// <para>
/// <b>为什么两次写入是连着做的</b>：规格要求"包成一条命令"——两次 <c>Set</c> 之间有一瞬
/// <c>沙砾 + 空隙 ≠ 容量</c>，而且每次 <c>Set</c> 都会刷一次 UI。RitsuLib 没有提供批量写入口，
/// 所以这里先读两个值、再在同一帧里连写两次（中间只做写入），把"不守恒窗口"压到一帧以内。
/// </para>
/// <para>
/// <b>倒置不改总量</b>：它只交换两个数，所以不会破坏不变式。
/// </para>
/// </remarks>
internal static class SandInvertCmd
{
    /// <summary>倒置的能量费用（已定稿：2 费）。</summary>
    public const int EnergyCost = 2;

    /// <summary>能不能倒置（角色正确 + 能量够）。</summary>
    public static bool CanUse(Player player)
    {
        if (player.Character is not Aeonglass)
        {
            return false;
        }

        return (player.PlayerCombatState?.Energy ?? 0) >= EnergyCost;
    }

    /// <summary>
    /// 执行倒置（<b>唯一入口</b>）。返回 <c>true</c> 表示真的换了。
    /// </summary>
    /// <param name="player">操作者。</param>
    /// <param name="sourceCard">
    /// 触发者：卡牌调用时传该卡（用于日志与将来的"卡牌联动判定"）；计数器点击传 <c>null</c>。
    /// </param>
    /// <remarks>拒绝的原因（角色不对 / 能量不足）都会打日志，方便实机排查。</remarks>
    public static async Task<bool> TryInvert(Player player, CardModel? sourceCard = null)
    {
        if (player.Character is not Aeonglass)
        {
            return false;
        }

        var energy = player.PlayerCombatState?.Energy ?? 0;
        if (energy < EnergyCost)
        {
            Const.Logger.Info($"[{Const.ModId}] [M1] 倒置被拒（能量不足：{energy} < {EnergyCost}）");

            // 用户口径：按下但倒不了 → 计数器宝珠变红 1 秒（反馈放在计数器里，命令这边只管通知）。
            SandCounterUi.Current?.NotifyInvertRejected();
            return false;
        }

        await PlayerCmd.LoseEnergy(EnergyCost, player);

        var sand = SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId);
        var gap = SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.VoidId);

        await SecondaryResourceCmd.Set(player, AeonglassSecondaryResources.SandId, gap);
        await SecondaryResourceCmd.Set(player, AeonglassSecondaryResources.VoidId, sand);

        // 回合内计数（给"翻转联动"类卡读；不拦任何操作）。
        SandTurnCounters.MarkInvert(player);

        // 本场累计（ANN-3 积卷 / RW-1 反冲这类"本场统计"的卡读它）。
        Systems.History.CombatHistory.RecordInvert(player);

        // 翻转通知（能力牌用；例：INV-3 惯性 = 每翻转一次抽 1 张）。
        await NotifyInvertListeners(player);

        var source = sourceCard is null ? "计数器" : sourceCard.Id.Entry;
        Const.Logger.Info(
            $"[{Const.ModId}] [M1] 倒置（来自 {source}）：沙砾 {sand}→{gap}、空隙 {gap}→{sand}（付 {EnergyCost} 费）"
            + $"｜本回合第 {SandTurnCounters.InvertsThisTurn(player)} 次");

        // 交换后"沙砾"就是原来的空隙值 ⇒ 计数器该落到的档位 = gap。
        // 计数器拿它播 180° 翻转（沙先倒空、落定后抬到新值）；框架推上来的 update 不会打断它。
        SandCounterUi.Current?.NotifyInvertSucceeded(gap);

        return true;
    }

    /// <summary>
    /// 把"发生了一次翻转"转发给该玩家身上实现了 <see cref="IAeonglassInvertListener" /> 的能力。
    /// </summary>
    /// <remarks>
    /// 单个监听者抛异常只记日志、不打断翻转本身（翻转是单入口，通知是附带的收益线）。
    /// </remarks>
    private static async Task NotifyInvertListeners(Player player)
    {
        var inverts = SandTurnCounters.InvertsThisTurn(player);
        foreach (var power in player.Creature.Powers.OfType<IAeonglassInvertListener>().ToList())
        {
            try
            {
                await power.OnSandInverted(new BlockingPlayerChoiceContext(), player, inverts);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] [M1] 翻转通知失败（{power.GetType().Name}）：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
