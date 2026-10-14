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
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>反冲</b>（CardRW1，差分）：1 费 · 基础 8 伤；
/// <b>永恒副</b>：本场回溯过 ⇒ 12 伤；<b>消逝副</b>：本场累计消耗 ≥6 沙砾 ⇒ 12 伤。（升级：基础 +3。）
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBacklash() : ModCardTemplate(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy),
    IDeckVariantCard
{
    private const decimal BaseDamage = 8m;
    private const decimal TriggerDamage = 12m;
    private const int SpentThreshold = 6;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(BaseDamage, ValueProp.Move), new DynamicVar("TriggerDamage", TriggerDamage)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var side = DeckVariants.SideOf(Owner, this);
        var triggered = side == DeckSide.A
            ? CombatHistory.RewindsThisCombat(Owner) > 0
            : CombatHistory.SandSpentThisCombat(Owner) >= SpentThreshold;

        var damage = triggered ? TriggerDamage : DynamicVars.Damage.BaseValue;
        Const.Logger.Info(
            $"[{Const.ModId}] 反冲（{(side == DeckSide.B ? "消逝副" : "永恒副")}）：触发={triggered} ⇒ {damage} 伤");

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
