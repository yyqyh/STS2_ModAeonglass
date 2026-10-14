using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>换刃</b>（CardINV2）：1 费 · 8 伤；<b>本回合翻转或消耗过沙砾</b>改为 11 伤。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBladeSwap() : AeonglassAttackTemplate(1, CardRarity.Uncommon, 8m, 3m)
{
    private const decimal TriggerDamage = 11m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(Damage, ValueProp.Move), new DynamicVar("TriggerDamage", TriggerDamage)];

    protected override decimal DamageThisPlay
        => SandTurnCounters.InvertsThisTurn(Owner) > 0 || SandTurnCounters.SpentSandThisTurn(Owner)
            ? TriggerDamage
            : base.DamageThisPlay;
}
