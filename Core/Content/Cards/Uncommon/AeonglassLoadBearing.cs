using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>承重</b>（CardGAP1）：1 费 · 间隙里每有 1 张牌：获得 3 挡（最多 9 挡；升级上限 +3）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassLoadBearing() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal BlockPerGapCard = 3m;
    private const decimal CapBase = 9m;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("BlockPerGap", BlockPerGapCard),
        new DynamicVar("Cap", CapBase),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var gap = TimeGap.CountOf(Owner);
        var cap = DynamicVars["Cap"].BaseValue;
        var block = Math.Min(gap * BlockPerGapCard, cap);

        Const.Logger.Info($"[{Const.ModId}] 承重：间隙 {gap} 张 ⇒ {block} 挡（上限 {cap}）");
        if (block > 0m)
        {
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Cap"].UpgradeValueBy(3m);

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("GapCount", TimeGap.CountOf(Owner).ToString());
    }
}
