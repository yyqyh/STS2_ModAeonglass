using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// 「手牌上限 ±N」能力的公共实现（GRW 结构成长线的四张牌 + UTL-6 时枢都用它）。
/// </summary>
/// <remarks>
/// <para>
/// <b>挂点</b>：RitsuLib 的手牌上限系统 —— <c>MaxHandSizeCalculator</c> 会把战斗里所有实现了
/// <see cref="IMaxHandSizeModifier" /> 的模型（能力正好在战斗钩子监听者名单里）跑一遍，
/// 再由 <c>MaxHandSizePatchInstaller</c> 把结果喂给本体各处"手牌上限"判定。
/// 本体 <c>CardPile.MaxCardsInHand</c> 是写死的常量属性（注释明说"功能上是常量，做成属性是为了模组工效学"），
/// 直接改它会伤到所有角色 ⇒ **必须走 RitsuLib 这条口**。
/// </para>
/// <para>
/// <b>不做加减记账</b>：加值每次都读 <c>Amount</c>，能力在场即生效、离场（战斗结束 / 被移除）即失效
/// —— 与「间隙容量」的读法完全一致。
/// </para>
/// </remarks>
public abstract class AeonglassHandLimitPower : ModPowerTemplate, IMaxHandSizeModifier
{
    /// <summary>增益类（绿框），数值显示出来。</summary>
    public override PowerType Type => PowerType.Buff;

    /// <summary>可以叠加（多张同类卡各给一份）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 本能力给所有者的手牌上限加值（默认 = <c>Amount</c>）。
    /// </summary>
    /// <remarks>可为负 —— UTL-6 时枢用 <c>Amount</c> 记能量，手牌代价是另一条（覆写本属性给 −1）。</remarks>
    protected virtual int HandLimitDelta => (int)Amount;

    /// <inheritdoc />
    public int ModifyMaxHandSize(Player player, int currentMaxHandSize)
        => ReferenceEquals(player.Creature, Owner)
            ? Math.Max(0, currentMaxHandSize + HandLimitDelta)
            : currentMaxHandSize;
}
