using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>余波</b>（CardPH5，差分）：1 费 · 基础 8 伤；
/// <b>永恒副</b>：沙砾≥4 时改为 10 伤 + 3 挡；<b>消逝副</b>：本回合消耗过沙砾时改为 12 伤。（升级：基础 +3。）
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAfterwave() : AeonglassAttackTemplate(1, CardRarity.Uncommon, 8m, 3m),
    IDeckVariantCard
{
    private const decimal VariantDamageA = 10m;
    private const decimal VariantDamageB = 12m;
    private const decimal VariantBlockA = 3m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(Damage, ValueProp.Move),
        new DynamicVar("VariantDamageA", VariantDamageA),
        new DynamicVar("VariantDamageB", VariantDamageB),
        new DynamicVar("VariantBlockA", VariantBlockA),
    ];

    protected override decimal DamageThisPlay
        => DeckVariants.SideOf(Owner, this) switch
        {
            DeckSide.A when SandPhases.Of(Owner) == SandPhase.Sand => VariantDamageA,
            DeckSide.B when SandTurnCounters.SpentSandThisTurn(Owner) => VariantDamageB,
            _ => base.DamageThisPlay,
        };

    /// <summary>永恒副满足条件时，额外那点格挡。</summary>
    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (DeckVariants.SideOf(Owner, this) == DeckSide.A
            && SandPhases.Of(Owner) == SandPhase.Sand)
        {
            await CreatureCmd.GainBlock(Owner.Creature, VariantBlockA, ValueProp.Move, cardPlay);
        }
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
