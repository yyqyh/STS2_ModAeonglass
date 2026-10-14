using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>中天</b>（CardPH1）：0 费 · 6 伤；<b>平衡档</b>（沙砾＝3）改为 8 伤。</summary>
/// <remarks>相位条件走 <see cref="SandPhases" /> 这一个读口，不自己拿数字比。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassZenith() : AeonglassAttackTemplate(0, CardRarity.Common, 6m, 3m)
{
    private const decimal BalanceDamage = 8m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(Damage, ValueProp.Move), new DynamicVar("BalanceDamage", BalanceDamage)];

    protected override decimal DamageThisPlay =>
        SandPhases.IsBalanced(Owner) ? BalanceDamage : base.DamageThisPlay;
}
