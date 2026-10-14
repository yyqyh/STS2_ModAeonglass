using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>存真</b>（GAP-3）：2 费 · 本回合你坍缩进去的副本<b>不带虚无</b>（它们能跨回合留存）。
/// </summary>
/// <remarks>
/// 这是本批最危险的一张：它临时关掉 M2 的节奏闸。若实机发现"存真 + 坍缩"能无限囤牌，
/// 降级方案是把"不带虚无"改成"最多 2 张不带虚无"（闸门位就是 <c>GapEtherealGuard</c>）。
/// 升级按卡表不变。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassPreserve() : ModCardTemplate(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Preserve", 1m)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        GapEtherealGuard.Arm(Owner);
        return Task.CompletedTask;
    }
}
