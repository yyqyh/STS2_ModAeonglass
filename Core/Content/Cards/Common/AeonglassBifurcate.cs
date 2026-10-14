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

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>双分</b>（CardPH3，差分）：0 费 · 3 挡 + 抽 1 张；<b>永恒副</b>：沙砾＝3 时抽 2 张；
/// <b>消逝副</b>：本回合消耗过沙砾时抽 2 张。（升级：格挡 +2。）
/// </summary>
/// <remarks>
/// 差分的两副条件各走一条已有的读口：<see cref="SandPhases.IsBalanced" />（相位）与
/// <see cref="SandTurnCounters.SpentSandThisTurn" />（本回合消耗过沙砾）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBifurcate() : ModCardTemplate(0, CardType.Skill, CardRarity.Common, TargetType.Self),
    IDeckVariantCard
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(3m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        var side = DeckVariants.SideOf(Owner, this);
        var bonusDraw = side == DeckSide.A
            ? SandPhases.IsBalanced(Owner)
            : SandTurnCounters.SpentSandThisTurn(Owner);

        await CardPileCmd.Draw(choiceContext, bonusDraw ? 2 : 1, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);

    // ============================ 差分 ============================

    /// <summary>两副目前没有额外"效果块"（差别只在抽牌张数，已在 <c>OnPlay</c> 里按副判）。</summary>
    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    /// <summary>文案 stem（两副各一条，见 <c>cards.json</c> 的 <c>.variant.a/b</c>）。</summary>
    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
