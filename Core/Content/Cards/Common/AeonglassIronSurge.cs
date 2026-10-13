using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>铁波</b>（CardBA4）：1 费 · 5 伤 · 5 挡（伤害与格挡都随升级涨）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassIronSurge() : AeonglassAttackTemplate(1, CardRarity.Common, 5m, 2m)
{
    /// <summary>额外的那点格挡（0 级 5，升级 +2 —— 与伤害同一档）。</summary>
    private const decimal BlockBase = 5m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(Damage, ValueProp.Move), new BlockVar(BlockBase, ValueProp.Move)];

    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
