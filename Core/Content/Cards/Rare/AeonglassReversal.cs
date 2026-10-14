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
/// <b>逆位</b>（CardPH9，差分）：2 费 · 基础 15 伤（+3）；
/// 满足条件时改为<b>12 伤打 2 次</b>（两副各一条触发条件）。
/// </summary>
/// <remarks>多段用两次 <c>DamageCmd.Attack</c> 顺序结算（每段各自吃格挡/增益钩子，与本体多段同义）。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassReversal() : ModCardTemplate(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy),
    IDeckVariantCard
{
    private const decimal BaseDamage = 15m;
    private const decimal SegmentDamage = 12m;
    private const int Segments = 2;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(BaseDamage, ValueProp.Move), new DynamicVar("SegmentDamage", SegmentDamage)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var side = DeckVariants.SideOf(Owner, this);
        var triggered = side == DeckSide.A
            ? SandPhases.Of(Owner) == SandPhase.Sand
            : SandTurnCounters.SpentSandThisTurn(Owner);

        if (!triggered)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
            return;
        }

        for (var i = 0; i < Segments; i++)
        {
            await DamageCmd.Attack(SegmentDamage)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
