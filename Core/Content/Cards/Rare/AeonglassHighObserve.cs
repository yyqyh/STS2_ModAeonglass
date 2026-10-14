using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>高维观测</b>（INT-6，能力，差分）：2 费 · 每回合开始时，若你上回合操作过意图，抽 1 张；
/// <b>消逝副</b>改为获得 1 点能量。
/// </summary>
/// <remarks>
/// 副在打出的那一刻定型（写进能力的 <c>EnergyVariant</c>）：能力会跨副存留，
/// 而"这张牌该给哪种收益"应该在它被打出时决定。升级按卡表不变。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassHighObserve() : ModCardTemplate(2, CardType.Power, CardRarity.Rare, TargetType.Self),
    IDeckVariantCard
{
    /// <summary>触发时给几张（抽牌 / 能量共用同一个数）。</summary>
    private const decimal PerTrigger = 1m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Observe", PerTrigger)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var energyVariant = DeckVariants.SideOf(Owner, this) == DeckSide.B;
        var power = await PowerCmd.Apply<AeonglassHighObservePower>(
            choiceContext, Owner.Creature, DynamicVars["Observe"].BaseValue, Owner.Creature, this);

        if (power is not null)
        {
            power.EnergyVariant = energyVariant;
        }

        Const.Logger.Info(
            $"[{Const.ModId}] 高维观测：按 {(energyVariant ? "消逝副（能量）" : "永恒副（抽牌）")} 挂载");
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
