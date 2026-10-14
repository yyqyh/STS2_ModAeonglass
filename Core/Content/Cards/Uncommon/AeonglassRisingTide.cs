using MegaCrit.Sts2.Core.Entities.Cards;
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
/// <b>涨潮</b>（CardPH4，差分）：1 费 · 基础 8 伤；
/// <b>永恒副</b>：沙砾≥4 时改为 12 伤；<b>消逝副</b>：本回合消耗过沙砾时改为 12 伤。（升级：基础 +3。）
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassRisingTide() : AeonglassAttackTemplate(1, CardRarity.Uncommon, 8m, 3m),
    IDeckVariantCard
{
    private const decimal TriggerDamage = 12m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(Damage, ValueProp.Move), new DynamicVar("TriggerDamage", TriggerDamage)];

    protected override decimal DamageThisPlay
    {
        get
        {
            var (side, triggered) = VariantTrigger();
            return side is not null && triggered ? TriggerDamage : base.DamageThisPlay;
        }
    }

    /// <summary>当前副 + 这一副的触发条件是否满足（不满足 / 非本副的差分条件就按基础值打）。</summary>
    private (DeckSide? Side, bool Triggered) VariantTrigger()
    {
        var side = DeckVariants.SideOf(Owner, this);
        var triggered = side == DeckSide.A
            ? SandPhases.Of(Owner) == SandPhase.Sand
            : SandTurnCounters.SpentSandThisTurn(Owner);
        return (side, triggered);
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
