using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>余震</b>（CardRW3）：1 费 · 6 挡（+2）；若你本场回溯过：改为 12 挡。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAftershock() : ModCardTemplate(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const decimal BaseBlock = 6m;
    private const decimal TriggerBlock = 12m;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(BaseBlock, ValueProp.Move), new DynamicVar("TriggerBlock", TriggerBlock)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var rewound = CombatHistory.RewindsThisCombat(Owner) > 0;
        var block = rewound ? TriggerBlock : DynamicVars.Block.BaseValue;

        Const.Logger.Info($"[{Const.ModId}] 余震：本场回溯过={rewound} ⇒ {block} 挡");
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["TriggerBlock"].UpgradeValueBy(2m);
    }
}
