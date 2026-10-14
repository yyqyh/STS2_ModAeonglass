using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>积卷</b>（CardANN3，v2）：1 费 · 抽 2 张（升级 3）；本场你每翻转一次沙漏：多抽 1 张（最多 3）。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassScrollStack() : ModCardTemplate(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    private const decimal DrawBase = 2m;
    private const decimal CapBase = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Draw", DrawBase), new DynamicVar("Cap", CapBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var inverts = CombatHistory.InvertsThisCombat(Owner);
        var extra = Math.Min(inverts, (int)DynamicVars["Cap"].BaseValue);
        var baseDraw = (int)DynamicVars["Draw"].BaseValue;
        var total = baseDraw + extra;

        Const.Logger.Info($"[{Const.ModId}] 积卷：本场翻转 {inverts} 次 ⇒ 抽 {total}（基础 {baseDraw} + {extra}）");
        await CardPileCmd.Draw(choiceContext, total, Owner);
    }

    /// <summary>v2：升级抬基础抽数（2 → 3），翻转上限固定 3 不再跟着涨。</summary>
    protected override void OnUpgrade() => DynamicVars["Draw"].UpgradeValueBy(1m);
}
