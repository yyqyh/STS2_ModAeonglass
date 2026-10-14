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

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>倾沙</b>（CardPH8，差分）：2 费 · 基础 15 伤（+3）；
/// <b>永恒副</b>：沙砾≥4 ⇒ 21 伤；<b>消逝副</b>：本回合消耗过沙砾 ⇒ 21 伤。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSandTilt() : ModCardTemplate(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy),
    IDeckVariantCard
{
    private const decimal BaseDamage = 15m;
    private const decimal TriggerDamage = 21m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(BaseDamage, ValueProp.Move), new DynamicVar("TriggerDamage", TriggerDamage)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var side = DeckVariants.SideOf(Owner, this);
        var triggered = side == DeckSide.A
            ? SandPhases.Of(Owner) == SandPhase.Sand
            : SandTurnCounters.SpentSandThisTurn(Owner);

        var damage = triggered ? TriggerDamage : DynamicVars.Damage.BaseValue;
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["TriggerDamage"].UpgradeValueBy(3m);
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
