using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>积卷</b>（CardANN3）：1 费 · 抽 1 张；本场你每翻转一次沙漏：多抽 1 张（最多 3；升级上限 +1）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassScrollStack() : ModCardTemplate(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const int BaseDraw = 1;
    private const decimal CapBase = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Cap", CapBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var inverts = CombatHistory.InvertsThisCombat(Owner);
        var extra = Math.Min(inverts, (int)DynamicVars["Cap"].BaseValue);
        var total = BaseDraw + extra;

        Const.Logger.Info($"[{Const.ModId}] 积卷：本场翻转 {inverts} 次 ⇒ 抽 {total}（基础 1 + {extra}）");
        await CardPileCmd.Draw(choiceContext, total, Owner);
    }

    protected override void OnUpgrade() => DynamicVars["Cap"].UpgradeValueBy(1m);
}
