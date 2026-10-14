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
/// <b>淬沙</b>（CardSD1，差分）：1 费 · 基础 8 伤；
/// <b>永恒副</b>：可消耗 1 沙砾改为 11 伤；<b>消逝副</b>：可消耗 3 沙砾改为 17 伤。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassQuench() : ModCardTemplate(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy),
    IDeckVariantCard
{
    private const decimal BaseDamage = 8m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(BaseDamage, ValueProp.Move), new DynamicVar("SpendDamage", 11m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var (spend, boosted) = DeckVariants.SideOf(Owner, this) == DeckSide.A ? (1, 11m) : (3, 17m);
        var damage = await SandSpend.TrySpendAsync(Owner, spend) ? boosted : BaseDamage + UpgradeDelta;

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    /// <summary>升级加的基础伤害。</summary>
    private decimal UpgradeDelta => CurrentUpgradeLevel * 3m;

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["SpendDamage"].UpgradeValueBy(3m);
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
