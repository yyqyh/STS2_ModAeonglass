using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Rewind;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>双线</b>（RW-4）：2 费 · 本场你可以额外回溯 1 次（代价照付：消耗全部沙砾）。
/// </summary>
/// <remarks>
/// 安全阀是代价而不是次数：回溯要"消耗全部沙砾归 0"，所以第二次回溯必须重新攒沙砾
/// （天然限流）。这条不能改成"免费回溯"。升级按卡表不变。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDualLine() : ModCardTemplate(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("ExtraRewind", 1m)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        RewindCombatLimit.GrantExtra(Owner, (int)DynamicVars["ExtraRewind"].BaseValue);
        return Task.CompletedTask;
    }
}
