using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Cards.Templates;

/// <summary>
/// 「底线件」攻击牌模板：<b>一次伤害 +（可选）一段额外效果</b>。
/// </summary>
/// <remarks>
/// <para>
/// 卡表里 BA 通道（无条件攻击）与 BB 通道（无条件格挡）共 20 张，绝大多数只有"一个主数值 + 可能的抽牌"。
/// 把重复部分收在这里 ⇒ 每张卡只剩"数值 + 额外效果"两件事，改数值只改构造参数。
/// </para>
/// <para>
/// ⚠️ 具体卡仍然各自一个类并带 <c>[RegisterCard]</c>：注册粒度是**具体类**，模板只是省样板。
/// </para>
/// </remarks>
public abstract class AeonglassAttackTemplate : ModCardTemplate
{
    private readonly decimal _upgradeDelta;

    /// <param name="cost">能量费用。</param>
    /// <param name="rarity">稀有度（卡表定的）。</param>
    /// <param name="damage">基础伤害。</param>
    /// <param name="upgradeDelta">升级加多少伤害。</param>
    protected AeonglassAttackTemplate(int cost, CardRarity rarity, decimal damage, decimal upgradeDelta)
        : base(cost, CardType.Attack, rarity, TargetType.AnyEnemy)
    {
        _upgradeDelta = upgradeDelta;
        Damage = damage;
    }

    /// <summary>基础伤害（给子类写额外效果时参考）。</summary>
    protected decimal Damage { get; }

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(Damage, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        await OnPlayExtra(choiceContext, cardPlay);
    }

    /// <summary>主效果之后的额外效果（默认没有）。</summary>
    protected virtual Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(_upgradeDelta);
}

/// <summary>「底线件」技能牌模板：<b>一次格挡 +（可选）一段额外效果</b>。</summary>
public abstract class AeonglassBlockTemplate : ModCardTemplate
{
    private readonly decimal _upgradeDelta;

    protected AeonglassBlockTemplate(int cost, CardRarity rarity, decimal block, decimal upgradeDelta)
        : base(cost, CardType.Skill, rarity, TargetType.Self)
    {
        _upgradeDelta = upgradeDelta;
        Block = block;
    }

    /// <summary>基础格挡（给子类参考）。</summary>
    protected decimal Block { get; }

    /// <summary>让"获得格挡"类判定认它（与 <c>AeonglassDefend</c> 一致）。</summary>
    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(Block, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await OnPlayExtra(choiceContext, cardPlay);
    }

    /// <summary>主效果之后的额外效果（默认没有）。</summary>
    protected virtual Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(_upgradeDelta);
}
