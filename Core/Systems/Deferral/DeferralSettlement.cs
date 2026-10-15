using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Aeonglass.Core.Systems.Deferral;

/// <summary>
/// 延宕的<b>到期结算</b>：把到点的欠账一次性兑付（走普通伤害 ⇒ 可被格挡）。
/// </summary>
/// <remarks>
/// ① 职责：挑出"窗口已到"的欠账、合成一笔伤害、清账、并按 `DeferralLedger.Settling` 防二次延宕。
/// ② 调用时机：玩家回合结束（<c>AeonglassDeferralPower.AfterSideTurnEnd</c>）。
/// ③ 依赖：<c>DeferralLedger</c>（账本）+ 本体伤害命令。
/// ④ ⚠️ 换战斗时旧账作废（不结算）；结算期间 <c>IsSettling</c> 为真，拦截钩子必须放行。
/// </remarks>
internal static class DeferralSettlement
{

    /// <summary>
    /// 玩家回合结束：兑付<b>到期</b>的欠账（一次性、走普通伤害 ⇒ 可被格挡）。
    /// </summary>
    /// <returns>这一下结算了多少（0 = 没有到期的）。</returns>
    public static async Task<decimal> PayDueAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (DeferralLedger.Combat is not { } combat || !DeferralLedger.Debts.TryGetValue(player, out var debts) || debts.Count == 0)
        {
            return 0m;
        }

        // 战斗换了 ⇒ 旧账作废（规则 3b：不结算）。
        debts.RemoveAll(debt => !ReferenceEquals(debt.Combat, combat));

        var turn = DeferralLedger.TurnOf(player);
        var due = debts.Where(debt => debt.DueTurn <= turn).ToList();
        if (due.Count == 0)
        {
            return 0m;
        }

        var total = due.Sum(debt => debt.Amount);
        var sources = string.Join(" + ", due.Select(debt => debt.Source));
        foreach (var debt in due)
        {
            debts.Remove(debt);
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Deferral} 延宕：到期结算 {total} 点（{sources}）⇒ 走普通伤害，可被格挡");

        DeferralLedger.Settling = true;
        try
        {
            await CreatureCmd.Damage(choiceContext, player.Creature, total, ValueProp.Move, null, null);
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] {Const.Prefix.Deferral} 延宕：结算失败（{ex.GetType().Name}: {ex.Message}）");
        }
        finally
        {
            DeferralLedger.Settling = false;
        }

        return total;
    }
}
