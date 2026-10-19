using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_Aeonglass.Core.Content.Cards.Templates;

/// <summary>一张卡的声明式配置：基础字段 + 变量 + 效果 + 升级 + 特性。</summary>
public sealed class AeonglassCardSpec
{
    public AeonglassCardSpec(int cost, CardType type, CardRarity rarity, TargetType target)
    {
        Cost = cost;
        Type = type;
        Rarity = rarity;
        Target = target;
    }

    public int Cost { get; }

    public CardType Type { get; }

    public CardRarity Rarity { get; }

    public TargetType Target { get; }

    public List<CardKeyword> Keywords { get; } = [];

    public List<CardTag> Tags { get; } = [];

    public List<CardVarSpec> Vars { get; } = [];

    public List<CardEffect> Effects { get; } = [];

    public List<CardUpgradeOp> Upgrades { get; } = [];

    public List<ICardFeature> Features { get; } = [];

    public bool GainsBlock { get; set; }

    public bool HasEnergyCostX { get; set; }

    public int MaxUpgradeLevel { get; set; } = 1;

    public bool Playable { get; set; } = true;

    public Func<AeonglassCardTemplate, CardLocation>? ResultLocation { get; set; }

    // ======================= 静态工厂 =======================

    public static AeonglassCardSpec Attack(
        int cost,
        CardRarity rarity,
        decimal damage,
        decimal upgradeDelta = 0m,
        TargetType target = TargetType.AnyEnemy,
        string hitFx = CardEffects.DefaultAttackFx)
    {
        var spec = new AeonglassCardSpec(cost, CardType.Attack, rarity, target);
        spec.Vars.Add(CardVarSpec.Damage(damage, upgradeDelta));
        spec.Effects.Add(CardEffects.Damage(hitFx: hitFx));
        return spec;
    }

    public static AeonglassCardSpec Block(
        int cost,
        CardRarity rarity,
        decimal block,
        decimal upgradeDelta = 0m,
        bool defendTag = true)
    {
        var spec = new AeonglassCardSpec(cost, CardType.Skill, rarity, TargetType.Self)
        {
            GainsBlock = true,
        };
        if (defendTag)
        {
            spec.Tags.Add(CardTag.Defend);
        }

        spec.Vars.Add(CardVarSpec.Block(block, upgradeDelta));
        spec.Effects.Add(CardEffects.GainBlock());
        return spec;
    }

    public static AeonglassCardSpec Skill(
        int cost,
        CardRarity rarity,
        TargetType target = TargetType.Self) =>
        new(cost, CardType.Skill, rarity, target);

    public static AeonglassCardSpec Power(int cost, CardRarity rarity) =>
        new(cost, CardType.Power, rarity, TargetType.Self);

    public static AeonglassCardSpec Status(
        int cost,
        CardRarity rarity,
        bool playable = false) =>
        new(cost, CardType.Status, rarity, TargetType.Self) { Playable = playable };

    // ======================= 链式修饰 =======================

    public AeonglassCardSpec Var(string name, decimal baseValue, decimal upgradeDelta = 0m)
    {
        Vars.Add(CardVarSpec.Value(name, baseValue, upgradeDelta));
        return this;
    }

    public AeonglassCardSpec DamageVar(
        decimal baseValue,
        decimal upgradeDelta = 0m,
        string name = "Damage")
    {
        Vars.Add(CardVarSpec.Damage(baseValue, upgradeDelta, name));
        return this;
    }

    public AeonglassCardSpec BlockVar(
        decimal baseValue,
        decimal upgradeDelta = 0m,
        string name = "Block")
    {
        Vars.Add(CardVarSpec.Block(baseValue, upgradeDelta, name));
        return this;
    }

    public AeonglassCardSpec Keyword(CardKeyword keyword)
    {
        Keywords.Add(keyword);
        return this;
    }

    public AeonglassCardSpec Tag(CardTag tag)
    {
        Tags.Add(tag);
        return this;
    }

    public AeonglassCardSpec Effect(CardEffect effect)
    {
        Effects.Add(effect);
        return this;
    }

    public AeonglassCardSpec Feature(ICardFeature feature)
    {
        Features.Add(feature);
        return this;
    }

    public AeonglassCardSpec UpgradeAddKeyword(CardKeyword keyword)
    {
        Upgrades.Add(CardUpgradeOp.AddKeyword(keyword));
        return this;
    }

    public AeonglassCardSpec UpgradeRemoveKeyword(CardKeyword keyword)
    {
        Upgrades.Add(CardUpgradeOp.RemoveKeyword(keyword));
        return this;
    }

    public AeonglassCardSpec UpgradeEnergy(int delta)
    {
        Upgrades.Add(CardUpgradeOp.EnergyCost(delta));
        return this;
    }

    public AeonglassCardSpec UpgradeVar(string name, decimal delta)
    {
        Upgrades.Add(CardUpgradeOp.Var(name, delta));
        return this;
    }

    // ======================= 运行时 =======================

    internal IEnumerable<DynamicVar> CreateVars() => Vars.Select(var => var.Create());

    internal void ApplyUpgrade(AeonglassCardTemplate card)
    {
        foreach (var var in Vars.Where(var => var.UpgradeDelta != 0m))
        {
            card.DynamicVars[var.Name].UpgradeValueBy(var.UpgradeDelta);
        }

        foreach (var upgrade in Upgrades)
        {
            upgrade.Apply(card);
        }
    }
}

/// <summary>一个卡面变量的声明：名称 + 工厂 + 升级增量。</summary>
public sealed record CardVarSpec(string Name, Func<DynamicVar> Create, decimal UpgradeDelta = 0m)
{
    public static CardVarSpec Damage(decimal baseValue, decimal upgradeDelta = 0m, string name = "Damage") =>
        new(name, () => new DamageVar(baseValue, ValueProp.Move), upgradeDelta);

    public static CardVarSpec Block(decimal baseValue, decimal upgradeDelta = 0m, string name = "Block") =>
        new(name, () => new BlockVar(baseValue, ValueProp.Move), upgradeDelta);

    public static CardVarSpec Value(string name, decimal baseValue, decimal upgradeDelta = 0m) =>
        new(name, () => new DynamicVar(name, baseValue), upgradeDelta);
}

/// <summary>一个升级操作（关键词 / 费用 / 变量 / 自定义）。</summary>
public sealed record CardUpgradeOp(string Key, Action<AeonglassCardTemplate> Apply)
{
    public static CardUpgradeOp AddKeyword(CardKeyword keyword) =>
        new($"keyword+{keyword}", card => card.AddKeyword(keyword));

    public static CardUpgradeOp RemoveKeyword(CardKeyword keyword) =>
        new($"keyword-{keyword}", card => card.RemoveKeyword(keyword));

    public static CardUpgradeOp EnergyCost(int delta) =>
        new($"energy{delta:+0;-0}", card => card.EnergyCost.UpgradeBy(delta));

    public static CardUpgradeOp Var(string name, decimal delta) =>
        new($"var:{name}{delta:+0;-0}", card => card.DynamicVars[name].UpgradeValueBy(delta));

    public static CardUpgradeOp Custom(string key, Action<AeonglassCardTemplate> apply) =>
        new($"custom:{key}", apply);
}

/// <summary>效果执行上下文。</summary>
public readonly record struct CardEffectContext(
    AeonglassCardTemplate Card,
    MegaCrit.Sts2.Core.Entities.Players.Player Player,
    MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext ChoiceContext,
    CardPlay Play)
{
    public DynamicVarSet Vars => Card.DynamicVars;

    public CardModel Model => Card;

    public MegaCrit.Sts2.Core.Entities.Creatures.Creature? Target => Play.Target;
}

/// <summary>带稳定 key 的一段效果。</summary>
public sealed record CardEffect(string Key, Func<CardEffectContext, Task> Apply);
