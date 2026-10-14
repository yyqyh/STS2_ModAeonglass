using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>泄沙</b>（CardSD4，差分）：1 费 · 基础 6 挡；
/// <b>永恒副</b>：可消耗 1 沙砾改为 9 挡；<b>消逝副</b>：可消耗 2 沙砾改为 12 挡。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSandRelease() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self),
    IDeckVariantCard
{
    private const decimal BaseBlock = 6m;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(BaseBlock, ValueProp.Move), new DynamicVar("SpendBlock", 9m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var (spend, boosted) = DeckVariants.SideOf(Owner, this) == DeckSide.A ? (1, 9m) : (2, 12m);
        var block = await SandSpend.TrySpendAsync(Owner, spend) ? boosted : DynamicVars.Block.BaseValue;

        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["SpendBlock"].UpgradeValueBy(3m);
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
