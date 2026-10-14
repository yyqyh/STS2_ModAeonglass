using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>定沙</b>（CardPH2）：0 费 · 6 挡；<b>平衡档</b>（沙砾＝3）改为 8 挡。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSandSet() : AeonglassBlockTemplate(0, CardRarity.Common, 6m, 3m)
{
    private const decimal BalanceBlock = 8m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(Block, ValueProp.Move), new DynamicVar("BalanceBlock", BalanceBlock)];

    protected override decimal BlockThisPlay => SandPhases.IsBalanced(Owner) ? BalanceBlock : base.BlockThisPlay;
}
