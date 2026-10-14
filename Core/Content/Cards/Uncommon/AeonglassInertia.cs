using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>惯性</b>（CardINV3，能力 · v2）：1 费 · 每当你翻转沙漏：抽 2 张牌（每回合 1 次；<b>升级不变</b>）。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassInertia() : ModCardTemplate(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal OnInvertDrawBase = 2m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("OnInvertDraw", OnInvertDrawBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassInertiaPower>(
            choiceContext, Owner.Creature, DynamicVars["OnInvertDraw"].BaseValue, Owner.Creature, this);

    /// <summary>v2：升级不抬数值（卡表口径）。</summary>
    protected override void OnUpgrade()
    {
    }
}
