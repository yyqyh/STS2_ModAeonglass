using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>撒播</b>（CardWTH2）：0 费 · 塞 1 张凋萎进怪物的弃牌堆（升级 ⇒ 2 张）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBroadcast() : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Wither", 1m)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 撒播：没有目标怪");
            return Task.CompletedTask;
        }

        var count = (int)DynamicVars["Wither"].BaseValue;
        Const.Logger.Info($"[{Const.ModId}] 撒播：{VoidPollution.Inject(target, count)}");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Wither"].UpgradeValueBy(1m);
}
