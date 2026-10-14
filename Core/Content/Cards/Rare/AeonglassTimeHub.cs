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
/// <b>时枢</b>（UTL-6，能力）：2 费 · 每回合开始时获得 1 点能量；<b>你的手牌上限 −1</b>。
/// </summary>
/// <remarks>
/// 每回合白拿 1 费是<b>长期打破零和</b>的（卡表 notes）⇒ 代价必须真实：手牌上限 −1 是常驻的结构代价，
/// 而且它会把 GRW 线"买容量"的收益直接抵消掉一半 —— 两条线互为对端。升级按卡表不变。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTimeHub() : ModCardTemplate(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("TurnEnergy", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassTimeHubPower>(
            choiceContext, Owner.Creature, DynamicVars["TurnEnergy"].BaseValue, Owner.Creature, this);
}
