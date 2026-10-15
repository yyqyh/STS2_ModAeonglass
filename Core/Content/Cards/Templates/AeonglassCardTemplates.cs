using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Stasis;
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

    /// <summary>
    /// 这一次实际打多少伤害（默认 = 卡面基础值）。
    /// </summary>
    /// <remarks>给"相位条件改数值"这类卡覆写（例：PH-1 中天在平衡档改打 8）—— 覆写它就等于改了整条出牌流程。</remarks>
    protected virtual decimal DamageThisPlay => DynamicVars.Damage.BaseValue;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DamageThisPlay)
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

    /// <summary>BB 通道就是"格挡件" ⇒ 带本体自己的 <see cref="CardTag.Defend" />（"下一张防御牌"类效果认它）。</summary>
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(Block, ValueProp.Move)];

    /// <summary>这一次实际拿多少格挡（默认 = 卡面基础值；给"相位条件改数值"的卡覆写）。</summary>
    /// <remarks>"下一张防御牌 +N"这类加成不在这里加 —— 那走本体的格挡钩子（见 <c>AeonglassNextDefensePower</c>）。</remarks>
    protected virtual decimal BlockThisPlay => DynamicVars.Block.BaseValue;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, BlockThisPlay, ValueProp.Move, cardPlay);
        await OnPlayExtra(choiceContext, cardPlay);
    }

    /// <summary>主效果之后的额外效果（默认没有）。</summary>
    protected virtual Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(_upgradeDelta);
}

/// <summary>
/// <b>搁浅攻击件模板</b>（STA 通道）：攻击件 + 「废弃后不进任何原版牌堆，改为搁浅 N 场战斗」。
/// </summary>
/// <remarks>
/// <para>
/// 2026-10-15 收样板：铭刻 / 封沙 / 破片 原来各自复制了 25 行三段样板
/// （<see cref="IStasisCard" /> + <c>[SavedProperty] CombatsLeft</c> + 落点覆写 + 打出后交给搁浅区的回调），
/// 现在只剩"费用 / 稀有度 / 数值 / 升级增量 / 搁浅场数"五个构造参数。
/// </para>
/// <para>
/// ⚠️ 搁浅未注册（<see cref="ExtraPileModules.StasisEnabled" /> 关）时自动退回本体默认落点 ⇒ 卡不会凭空消失。
/// </para>
/// </remarks>
public abstract class AeonglassStasisAttackTemplate : AeonglassAttackTemplate, IStasisCard
{
    private int _combatsLeft;

    /// <param name="cost">能量费用。</param>
    /// <param name="rarity">稀有度（卡表定的）。</param>
    /// <param name="damage">基础伤害。</param>
    /// <param name="upgradeDelta">升级加多少伤害。</param>
    /// <param name="strandCombats">搁浅几场战斗（卡面"搁浅 N"）。</param>
    protected AeonglassStasisAttackTemplate(
        int cost,
        CardRarity rarity,
        decimal damage,
        decimal upgradeDelta,
        int strandCombats)
        : base(cost, rarity, damage, upgradeDelta)
        => StrandCombats = strandCombats;

    /// <summary>搁浅几场战斗（卡面"搁浅 N"）。</summary>
    protected int StrandCombats { get; }

    /// <summary>还剩几次"战斗结束"才归队（内部按 X + 1 存，打出当场那次不计）。</summary>
    [SavedProperty]
    public int CombatsLeft
    {
        get => _combatsLeft;
        set => _combatsLeft = value;
    }

    /// <summary>打出后不回任何原版牌堆（搁浅没注册时退回本体默认行为）。</summary>
    protected override CardLocation GetResultLocationForCardPlay()
        => ExtraPileModules.StasisEnabled && StasisStore.IsReady
            ? new CardLocation(Owner, PileType.None, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, this) || Owner is null)
        {
            return;
        }

        if (!ExtraPileModules.StasisEnabled || !StasisStore.IsReady || DeckVersion is not { } master)
        {
            return;
        }

        DeckVersion = null;
        await StasisReturn.SendToZone(Owner, master, StrandCombats);
    }
}

/// <summary>同上，格挡件版本（STA 通道的防御件）。</summary>
public abstract class AeonglassStasisBlockTemplate : AeonglassBlockTemplate, IStasisCard
{
    private int _combatsLeft;

    /// <param name="cost">能量费用。</param>
    /// <param name="rarity">稀有度（卡表定的）。</param>
    /// <param name="block">基础格挡。</param>
    /// <param name="upgradeDelta">升级加多少格挡。</param>
    /// <param name="strandCombats">搁浅几场战斗（卡面"搁浅 N"）。</param>
    protected AeonglassStasisBlockTemplate(
        int cost,
        CardRarity rarity,
        decimal block,
        decimal upgradeDelta,
        int strandCombats)
        : base(cost, rarity, block, upgradeDelta)
        => StrandCombats = strandCombats;

    /// <summary>搁浅几场战斗（卡面"搁浅 N"）。</summary>
    protected int StrandCombats { get; }

    /// <summary>还剩几次"战斗结束"才归队（内部按 X + 1 存，打出当场那次不计）。</summary>
    [SavedProperty]
    public int CombatsLeft
    {
        get => _combatsLeft;
        set => _combatsLeft = value;
    }

    /// <summary>打出后不回任何原版牌堆（搁浅没注册时退回本体默认行为）。</summary>
    protected override CardLocation GetResultLocationForCardPlay()
        => ExtraPileModules.StasisEnabled && StasisStore.IsReady
            ? new CardLocation(Owner, PileType.None, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, this) || Owner is null)
        {
            return;
        }

        if (!ExtraPileModules.StasisEnabled || !StasisStore.IsReady || DeckVersion is not { } master)
        {
            return;
        }

        DeckVersion = null;
        await StasisReturn.SendToZone(Owner, master, StrandCombats);
    }
}

/// <summary>
/// <b>能力卡模板</b>（Power 通道）：打出即挂一个能力，<b>默认升级不抬数值</b>（卡表里能力卡多数"升级不变"）。
/// </summary>
/// <remarks>
/// 2026-10-15 收样板：惯性 / 时枢 / 贷时 / 相位律 原来各写一遍类声明 + 空 <c>OnUpgrade</c>；
/// 现在子类只剩"费用 / 稀有度 / 挂的能力 / 点数"。
/// 数值可升级的能力卡（例如"升级 +1"）覆写 <see cref="ModCardTemplate.OnUpgrade" /> 即可。
/// </remarks>
public abstract class AeonglassPowerCardTemplate : ModCardTemplate
{
    /// <param name="cost">能量费用。</param>
    /// <param name="rarity">稀有度（卡表定的）。</param>
    protected AeonglassPowerCardTemplate(int cost, CardRarity rarity)
        : base(cost, CardType.Power, rarity, TargetType.Self)
    {
    }

    protected override HashSet<CardTag> CanonicalTags => [];

    /// <summary>默认升级不抬数值（卡表口径：能力卡多数升级不变）。</summary>
    protected override void OnUpgrade()
    {
    }
}

/// <summary>
/// <b>差分卡公共样板</b>（永时副 A / 消逝副 B）：<c>a</c>/<c>b</c> 文案 stem + 卡面差分文本注入 +
/// "此刻在哪一副"的快捷判定 —— 原来每张差分卡各写一遍这三件事。
/// </summary>
/// <remarks>
/// <para>
/// 判定的<b>唯一单点</b>在 <c>DeckPiles.DeckIndexOf</c>（经 <c>DeckVariants.SideOf</c>）
/// ⇒ 卡里只准用 <see cref="Side" /> / <see cref="IsVariantB" />，不要自己拿牌组或副名去比。
/// </para>
/// <para>
/// C# 单继承 ⇒ 三种形状各一个基类：本类用于"两副各走一条路、效果直接写在 <c>OnPlay</c> 里"的卡（播种）；
/// <see cref="AeonglassVariantAttackTemplate" />（潮汐）与 <see cref="AeonglassVariantBlockTemplate" />（堤岸）
/// 用于"数值件 + 差分"。
/// </para>
/// </remarks>
public abstract class AeonglassVariantCardTemplate : ModCardTemplate, IDeckVariantCard
{
    /// <param name="cost">能量费用。</param>
    /// <param name="type">卡牌类型。</param>
    /// <param name="rarity">稀有度（卡表定的）。</param>
    /// <param name="target">指向类型。</param>
    protected AeonglassVariantCardTemplate(int cost, CardType type, CardRarity rarity, TargetType target)
        : base(cost, type, rarity, target)
    {
    }

    /// <summary>本卡此刻在哪一副（A = 永时副 / B = 消逝副）。</summary>
    protected DeckSide Side => DeckVariants.SideOf(Owner, this);

    /// <summary>是不是消逝副（B）。</summary>
    protected bool IsVariantB => Side == DeckSide.B;

    /// <summary>打出时执行"当前副"的差分块（没有块就什么都不做）。</summary>
    protected Task PlayVariantAsync(CardPlay cardPlay, PlayerChoiceContext choiceContext)
        => DeckVariants.PlayVariantAsync(new DeckEffectContext(Owner, this, cardPlay, choiceContext));

    /// <summary>默认没有差分块（只有文案差分、效果写在 <c>OnPlay</c> 里的卡用它）。</summary>
    public virtual IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    /// <summary>文案 stem：A ⇒ <c>a</c>，B ⇒ <c>b</c>（本地化键 <c>{Id}.variant.a</c> / <c>.b</c>）。</summary>
    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    /// <summary>把当前副那段差分文案注入卡面（<c>{Variant}</c>）。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}

/// <summary>差分 + 攻击件（例：潮汐）：数值走 <see cref="AeonglassAttackTemplate" />，差分走上面那套。</summary>
/// <remarks>额外效果默认就是"跑当前副的差分块" ⇒ 子类通常只需覆写 <see cref="VariantBlocks" />。</remarks>
public abstract class AeonglassVariantAttackTemplate : AeonglassAttackTemplate, IDeckVariantCard
{
    protected AeonglassVariantAttackTemplate(int cost, CardRarity rarity, decimal damage, decimal upgradeDelta)
        : base(cost, rarity, damage, upgradeDelta)
    {
    }

    /// <summary>本卡此刻在哪一副（A = 永时副 / B = 消逝副）。</summary>
    protected DeckSide Side => DeckVariants.SideOf(Owner, this);

    /// <summary>是不是消逝副（B）。</summary>
    protected bool IsVariantB => Side == DeckSide.B;

    public virtual IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    /// <summary>主伤害之后自动跑"当前副"的差分块。</summary>
    protected override Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => DeckVariants.PlayVariantAsync(new DeckEffectContext(Owner, this, cardPlay, choiceContext));

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}

/// <summary>差分 + 格挡件（例：堤岸）：格挡走 <see cref="AeonglassBlockTemplate" />，差分走同一套。</summary>
public abstract class AeonglassVariantBlockTemplate : AeonglassBlockTemplate, IDeckVariantCard
{
    protected AeonglassVariantBlockTemplate(int cost, CardRarity rarity, decimal block, decimal upgradeDelta)
        : base(cost, rarity, block, upgradeDelta)
    {
    }

    /// <summary>本卡此刻在哪一副（A = 永时副 / B = 消逝副）。</summary>
    protected DeckSide Side => DeckVariants.SideOf(Owner, this);

    /// <summary>是不是消逝副（B）。</summary>
    protected bool IsVariantB => Side == DeckSide.B;

    public virtual IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    /// <summary>主格挡之后自动跑"当前副"的差分块。</summary>
    protected override Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => DeckVariants.PlayVariantAsync(new DeckEffectContext(Owner, this, cardPlay, choiceContext));

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
