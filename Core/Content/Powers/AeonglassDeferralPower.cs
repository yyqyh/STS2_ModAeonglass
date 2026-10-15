using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Deferral;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>延宕</b>（堤岸的消逝副挂的能力，<c>Amount</c> = 延宕回合数 X）：
/// 打出它的那一回合（含随后的敌人回合）你受到的伤害，<b>X 回合后一次性结算</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>拦截点</b>：<see cref="ModifyHpLostBeforeOsty" /> —— 它拿到的 <c>amount</c> 已经是"扣完格挡之后真正会掉的血"，
/// 所以：① 格挡照常吃掉它该吃的那部分（延宕的是"躲不过的那部分"）；
/// ② 返回 0 就是真的不掉血 ⇒ 不需要"先掉再补"那种会牵动死亡判定的写法。
/// </para>
/// <para>
/// <b>只延宕敌人打过来的伤害</b>：自伤（例：双刃的失去生命）与本 mod 自己的凋萎惩戒（<c>Unpowered</c>）不算
/// "你受到的伤害"，否则延宕会被拿去刷状态伤害。
/// </para>
/// <para>
/// <b>寿命</b>：能力一直留在场上，直到"没有未结清的欠账、且窗口已关"才自毁 ——
/// 这样图标上的数字始终是"还有几回合到账"，玩家看得见这笔账。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassDeferralPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>上次看到的累计回合数（用来算"这一次实际加了几回合"）。</summary>
    private decimal _seenAmount;

    /// <summary>
    /// 能力一挂上就<b>新增一个延宕实例</b>（规则 5c：每次获得＝独立实例）。
    /// <c>Amount</c> 是叠加后的总量，所以按差值取"这一次加了几回合"。
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        var delta = Amount - _seenAmount;
        _seenAmount = Amount;
        if (delta <= 0m)
        {
            delta = Amount; // 拿不到增量时退回"按当前总量开一次窗口"（例如回溯回档后重新挂上）。
        }

        if (Owner.Player is { } player)
        {
            DeferralLedger.Arm(player, (int)delta);
        }

        return Task.CompletedTask;
    }

    /// <summary>窗口内的伤害：记成欠账并把这一下压成 0（不掉血）。</summary>
    public override decimal ModifyHpLostBeforeOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (amount <= 0m
            || !ReferenceEquals(target, Owner)
            || Owner.Player is not { } player
            || !DeferralLedger.IsIntercepting(player))
        {
            return amount;
        }

        // 只认"敌人打过来的"：自伤与凋萎惩戒（Unpowered）不进这条账。
        if (dealer?.Monster is not { } monster)
        {
            return amount;
        }

        DeferralLedger.Record(player, amount, monster.Id.Entry);
        return 0m;
    }

    /// <summary>玩家回合结束：兑付到期的欠账；账清了、窗口也关了 ⇒ 自己离场。</summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner) || Owner.Player is not { } player)
        {
            return;
        }

        await DeferralSettlement.PayDueAsync(choiceContext, player);

        if (DeferralLedger.HasOpenDebt(player) || DeferralLedger.HasUnsettledInstance(player))
        {
            return;
        }

        var left = Amount;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Deferral} 延宕：账已结清 ⇒ 能力离场（-{left}）");
        await PowerCmd.Apply<AeonglassDeferralPower>(choiceContext, Owner, -left, Owner, null);
    }
}
