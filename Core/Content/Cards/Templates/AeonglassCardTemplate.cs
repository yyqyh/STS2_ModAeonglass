using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Bootstrap;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Stasis;

namespace STS2_Aeonglass.Core.Content.Cards.Templates;

/// <summary>统一卡牌基类：Spec 提供声明，复杂卡可继续覆写 OnPlay / OnPlayExtra。</summary>
public abstract class AeonglassCardTemplate : ModCardTemplate, IDeckVariantCard
{
    protected AeonglassCardTemplate(AeonglassCardSpec spec)
        : base(spec.Cost, spec.Type, spec.Rarity, spec.Target)
    {
        Spec = spec;
    }

    protected AeonglassCardSpec Spec { get; }

    /// <summary>测试接缝：读取本卡的声明式配置。</summary>
    internal AeonglassCardSpec SpecForTests => Spec;

    public bool HasVariant => Spec.Features.OfType<VariantFeature>().Any();

    public override IEnumerable<CardKeyword> CanonicalKeywords => Spec.Keywords;

    protected override HashSet<CardTag> CanonicalTags => Spec.Tags.ToHashSet();

    protected override IEnumerable<DynamicVar> CanonicalVars => Spec.CreateVars();

    public override bool GainsBlock => Spec.GainsBlock;

    protected override bool HasEnergyCostX => Spec.HasEnergyCostX;

    public override int MaxUpgradeLevel => Spec.MaxUpgradeLevel;

    protected override bool IsPlayable => Spec.Playable;

    /// <summary>本卡此刻在哪一副（判定单点仍是 DeckVariants）。</summary>
    protected DeckSide Side => DeckVariants.SideOf(Owner, this);

    protected bool IsVariantB => Side == DeckSide.B;

    protected virtual decimal DamageThisPlay => DynamicVars["Damage"].BaseValue;

    protected virtual decimal BlockThisPlay => DynamicVars["Block"].BaseValue;

    internal decimal ResolveDamageThisPlay(string var = "Damage") =>
        var == "Damage" ? DamageThisPlay : DynamicVars[var].BaseValue;

    internal decimal ResolveBlockThisPlay(string var = "Block") =>
        var == "Block" ? BlockThisPlay : DynamicVars[var].BaseValue;

    protected override void OnUpgrade() => Spec.ApplyUpgrade(this);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RunSpecEffectsAsync(choiceContext, cardPlay);
        await OnPlayExtra(choiceContext, cardPlay);
    }

    /// <summary>主效果之后的逃生口；复杂线性效果可继续覆写它。</summary>
    protected virtual Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        Task.CompletedTask;

    /// <summary>只跑 Spec 的 Effects + Features（复杂卡覆写 OnPlay 时可按需调用）。</summary>
    protected async Task RunSpecEffectsAsync(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner is not { } player)
        {
            return;
        }

        var context = new CardEffectContext(this, player, choiceContext, cardPlay);
        foreach (var effect in Spec.Effects)
        {
            await effect.Apply(context);
        }

        foreach (var feature in Spec.Features)
        {
            await feature.OnPlayAsync(context);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, this) || Owner is not { } player)
        {
            return;
        }

        var context = new CardEffectContext(this, player, choiceContext, cardPlay);
        foreach (var feature in Spec.Features)
        {
            await feature.AfterCardPlayedAsync(context);
        }
    }

    protected override CardLocation GetResultLocationForCardPlay()
    {
        foreach (var feature in Spec.Features)
        {
            if (feature.ModifyResultLocation(this) is { } location)
            {
                return location;
            }
        }

        return Spec.ResultLocation?.Invoke(this) ?? base.GetResultLocationForCardPlay();
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        foreach (var feature in Spec.Features)
        {
            feature.AddDescriptionArgs(description, this);
        }
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) =>
        Spec.Features.OfType<VariantFeature>().FirstOrDefault()?.VariantBlocks(side, context) ?? [];

    public string? VariantTextStem(DeckSide side) =>
        HasVariant
            ? Spec.Features.OfType<VariantFeature>().First().VariantTextStem(side)
            : null;
}

/// <summary>
/// 搁浅卡基类：唯一保留的中间基类（<c>IStasisCard</c> 需要卡上可序列化的倒计时字段）。
/// </summary>
public abstract class AeonglassStasisCardTemplate : AeonglassCardTemplate, IStasisCard
{
    private int _combatsLeft;

    protected AeonglassStasisCardTemplate(AeonglassCardSpec spec, int strandCombats)
        : base(spec)
    {
        StrandCombats = strandCombats;
        StrandLevel = strandCombats;
    }

    protected int StrandCombats { get; }

    private int _strandLevel;

    /// <summary>
    /// 下一次归队要搁浅几场（「归岸」会在每次打出后 +1；其余卡恒等于构造值）。
    /// </summary>
    /// <remarks>
    /// 存在<b>母本</b>上（战斗副本不保存）⇒ 跨战斗 / 读档都保留成长；构造时用快照值兜旧存档。
    /// 只有「归岸」会写它，其它搁浅卡读到的永远是 <see cref="StrandCombats" />。
    /// <para>
    /// ⚠️ 2026-10-21：旧存档没有这个 <c>[SavedProperty]</c> 时会读到 0 ⇒ getter 回退到
    /// <see cref="StrandCombats" />，否则卡面「搁浅N」会整行消失（裸关键词已按本体重放写法关闭）。
    /// </para>
    /// </remarks>
    [SavedProperty]
    public int StrandLevel
    {
        get => _strandLevel > 0 ? _strandLevel : StrandCombats;
        set => _strandLevel = value;
    }

    /// <summary>本次送入搁浅区要用的场数；「归岸」可覆写做额外结算。</summary>
    protected virtual int StrandCombatsForSend() => StrandLevel;

    /// <summary>送入搁浅区之后的追加结算（母本已入堆）；默认无。</summary>
    protected virtual void AfterSentToStasis(CardModel master)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Append(StasisKeyword.Value).Distinct();

    [SavedProperty]
    public int CombatsLeft
    {
        get => _combatsLeft;
        set
        {
            _combatsLeft = value;
            if (DynamicVars.Any(pair => pair.Key == "Combats"))
            {
                DynamicVars["Combats"].BaseValue = Math.Max(0, value);
            }
        }
    }

    protected override CardLocation GetResultLocationForCardPlay() =>
        ExtraPileModules.StasisEnabled && StasisStore.IsReady
            ? new CardLocation(Owner, PileType.None, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);

        if (!ReferenceEquals(cardPlay.Card, this) || Owner is null)
        {
            return;
        }

        if (!ExtraPileModules.StasisEnabled || !StasisStore.IsReady || DeckVersion is not { } master)
        {
            return;
        }

        DeckVersion = null;
        await StasisReturn.SendToZone(Owner, master, StrandCombatsForSend());
        AfterSentToStasis(master);
    }
}
