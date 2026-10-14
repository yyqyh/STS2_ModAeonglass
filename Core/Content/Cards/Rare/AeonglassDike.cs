using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>堤坝</b>（CardSD6）：1 费 · 9 挡（+3）；可消耗 2 沙砾再获得 6 挡。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDike() : ModCardTemplate(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const decimal BaseBlock = 9m;
    private const int SpendAmount = 2;
    private const decimal ExtraBlock = 6m;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(BaseBlock, ValueProp.Move), new DynamicVar("ExtraBlock", ExtraBlock)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        if (await SandSpend.TrySpendAsync(Owner, SpendAmount))
        {
            await CreatureCmd.GainBlock(Owner.Creature, ExtraBlock, ValueProp.Move, cardPlay);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}
