using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>贷时</b>（ETR-3，能力）：1 费 · 每回合开始时借 1 点能量（本回合 +1、下回合 −1）—— 可重复。
/// </summary>
/// <remarks>
/// 借贷长线净值 0（借的下回合要还）：这张牌买的是<b>把能量往前挪</b>的能力，
/// 也就是"这一回合能不能多打一张"的节奏。升级按卡表不变。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassLoanTime() : ModCardTemplate(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Borrow", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassLoanTimePower>(
            choiceContext, Owner.Creature, DynamicVars["Borrow"].BaseValue, Owner.Creature, this);
}
