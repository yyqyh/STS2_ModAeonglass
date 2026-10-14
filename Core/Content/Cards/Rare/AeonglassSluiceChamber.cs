using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>闸室</b>（CardGAP4）：2 费 · 6 挡；间隙每有 1 张牌额外 +2 挡（最多 14；升级上限 +4）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSluiceChamber() : ModCardTemplate(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const decimal BaseBlock = 6m;
    private const decimal PerGapBlock = 2m;
    private const decimal CapBase = 14m;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(BaseBlock, ValueProp.Move),
        new DynamicVar("PerGapBlock", PerGapBlock),
        new DynamicVar("Cap", CapBase),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var gap = TimeGap.CountOf(Owner);
        var extra = Math.Min(gap * PerGapBlock, DynamicVars["Cap"].BaseValue);
        var block = BaseBlock + extra;

        Const.Logger.Info($"[{Const.ModId}] 闸室：间隙 {gap} 张 ⇒ 额外 +{extra}（合计 {block}）");
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars["Cap"].UpgradeValueBy(4m);
}
