using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「你的下一张防御牌 +N 点格挡」（BB-4 垒壁引入的可见 buff）。</summary>
/// <remarks>
/// <para>
/// <b>挂点</b>：本体的格挡修正钩子 —— <c>Hook.ModifyBlock</c> 会遍历监听者问
/// <see cref="ModifyBlockAdditive" />（本体的「敏捷」就是这条）。这里在**一张防御牌获得格挡**时把
/// 自己的 <c>Amount</c> 加上去。
/// </para>
/// <para>
/// <b>用掉即移除</b>：加法发生在钩子里（同步），而移除要发命令（异步）⇒ 在加法那一刻只置个标记，
/// 等本体紧接着调 <see cref="AfterBlockGained" />（<c>Hook.AfterBlockGained</c>）时再把自己按负量移除。
/// 负量移除是本体的既有写法（酒狐的 <c>MaterialCmd</c> 也这么做）。
/// </para>
/// <para>
/// <b>怎么判"防御牌"</b>：用卡上的 public 属性 <c>GainsBlock</c>（本体自己也是靠它判定"这张牌给格挡"）。
/// 不用 <c>CanonicalTags</c> —— 那是 protected，Power 侧读不到。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassNextDefensePower : ModPowerTemplate
{
    /// <summary>增益类（绿框），数值可见。</summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>数值要显示出来（可叠加）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _appliedThisBlock;

    /// <summary>防御牌获得格挡时，把待生效的加成加上去。</summary>
    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (Amount <= 0m
            || !ReferenceEquals(target, Owner)
            || cardSource is not { GainsBlock: true })
        {
            return block;
        }

        _appliedThisBlock = true;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 垒壁：防御牌 {cardSource.Id.Entry} 额外 +{Amount} 格挡");
        return block + Amount;
    }

    /// <summary>格挡真的到手之后，把这次加成消耗掉（按负量移除）。</summary>
    /// <remarks>
    /// 本体这个钩子**不给 <c>PlayerChoiceContext</c>**（见 <c>Hook.AfterBlockGained</c>）⇒ 自己造一个阻塞式上下文。
    /// </remarks>
    public override Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (!_appliedThisBlock)
        {
            return Task.CompletedTask;
        }

        _appliedThisBlock = false;
        var spent = Amount;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 垒壁：加成已用掉（-{spent}）");
        return PowerCmd.Apply<AeonglassNextDefensePower>(
            new BlockingPlayerChoiceContext(), Owner, -spent, Owner, cardSource);
    }
}
