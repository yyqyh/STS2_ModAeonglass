using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>坝</b>（CardPH6）：0 费 · 6 挡；<b>沙砾侧</b>（沙砾≥4）改为 9 挡。（升级：基础 +3。）</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDam() : AeonglassBlockTemplate(0, CardRarity.Uncommon, 6m, 3m)
{
    private const decimal GravelBlock = 9m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(Block, ValueProp.Move), new DynamicVar("GravelBlock", GravelBlock)];

    protected override decimal BlockThisPlay
        => SandPhases.Of(Owner) == SandPhase.Sand ? GravelBlock : base.BlockThisPlay;
}
