using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「本回合下一张牌费用 −N」（UTL-2 省钟）。</summary>
/// <remarks>
/// <para>
/// <b>挂点</b>：本体的费用钩子 —— <c>Hook.&lt;TryModifyEnergyCostInCombat&gt;</c> 会遍历监听者问
/// <see cref="TryModifyEnergyCostInCombat" />。
/// </para>
/// <para>
/// <b>为什么不在这里"消耗"</b>：费用钩子会被 UI 预览反复调用（算显示价格），在里面扣次数会扣错 ——
/// 所以这里只记下"被减费的那张牌"，等它真的**被打出**后（<see cref="AfterCardPlayed" />）再移除自己。
/// </para>
/// <para>
/// <b>本回合限定</b>：<see cref="AfterSideTurnEnd" /> 里没用到也作废（口径是"本回合下一张牌"）。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassNextCardCostPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>最近被减费的那张牌（用来判断"它是不是真的被打出了"）。</summary>
    private CardModel? _discounted;

    /// <summary>费用修正：把所有者的牌费用减 <c>Amount</c>（不低于 0）。</summary>
    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Amount <= 0m || !ReferenceEquals(card.Owner?.Creature, Owner))
        {
            return false;
        }

        modifiedCost = Math.Max(0m, originalCost - Amount);
        _discounted = card;
        return modifiedCost != originalCost;
    }

    /// <summary>被减费的那张牌打出了 ⇒ 用掉本能力。</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_discounted is null || !ReferenceEquals(cardPlay.Card, _discounted))
        {
            return;
        }

        _discounted = null;
        var spent = Amount;
        Const.Logger.Info($"[{Const.ModId}] 省钟：{cardPlay.Card.Id.Entry} 吃到减费，能力用掉（-{spent}）");
        await PowerCmd.Apply<AeonglassNextCardCostPower>(choiceContext, Owner, -spent, Owner, cardPlay.Card);
    }

    /// <summary>本回合没用到 ⇒ 作废（"本回合下一张牌"的口径）。</summary>
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Amount <= 0m || !participants.Contains(Owner))
        {
            return;
        }

        _discounted = null;
        var expired = Amount;
        Const.Logger.Info($"[{Const.ModId}] 省钟：本回合没用掉 ⇒ 作废（-{expired}）");
        await PowerCmd.Apply<AeonglassNextCardCostPower>(choiceContext, Owner, -expired, Owner, null);
    }
}
