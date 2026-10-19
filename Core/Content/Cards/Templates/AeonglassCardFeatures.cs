using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;

using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Content.Cards.Templates;

/// <summary>可组合的卡牌特性：出牌后效果 / 落点 / 文案注入 / 差分块。</summary>
public interface ICardFeature
{
    Task OnPlayAsync(CardEffectContext context) => Task.CompletedTask;

    Task AfterCardPlayedAsync(CardEffectContext context) => Task.CompletedTask;

    CardLocation? ModifyResultLocation(AeonglassCardTemplate card) => null;

    void AddDescriptionArgs(LocString description, AeonglassCardTemplate card)
    {
    }

    IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    string? VariantTextStem(DeckSide side) => null;
}

/// <summary>差分特性：主效果之后跑差分块，并把当前副的文案注入卡面。</summary>
public sealed class VariantFeature : ICardFeature
{
    private readonly Func<DeckSide, DeckEffectContext, IReadOnlyList<DeckEffectBlock>> _blocks;
    private readonly bool _runBlocks;

    public VariantFeature(
        Func<DeckSide, DeckEffectContext, IReadOnlyList<DeckEffectBlock>>? blocks = null,
        bool runBlocks = true)
    {
        _blocks = blocks ?? ((_, _) => []);
        _runBlocks = runBlocks;
    }

    public Task OnPlayAsync(CardEffectContext context) =>
        _runBlocks
            ? DeckVariants.PlayVariantAsync(
                new DeckEffectContext(context.Player, context.Card, context.Play, context.ChoiceContext))
            : Task.CompletedTask;

    public void AddDescriptionArgs(LocString description, AeonglassCardTemplate card) =>
        DeckVariants.InjectVariantText(description, card);

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) =>
        _blocks(side, context);

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";
}
