using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>积卷</b>（CardANN3，v2 + delta 20261015）· <b>1 费 · Uncommon · 技能</b> —— 抽 2 张牌（升级 3）；本场你每翻转一次沙漏：多抽 1 张牌（<b>最多 2 张</b>）。
/// </summary>
/// <remarks>
/// ① 效果：基础抽数 + min(本场翻转次数, 上限 2)；升级只抬基础抽数（2 → 3），<b>上限固定 2</b>。
/// ② 差分：无差分。
/// ③ 依赖：<c>CombatHistory.InvertsThisCombat</c>（本场翻转次数）。
/// ④ ⚠️ 2026-10-15 delta 把上限 3 → 2 且改成定值（升级不再抬上限）—— 代码里原来还在抬上限，已按 delta 修掉。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassScrollStack() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal DrawBase = 2m;
    private const decimal CapBase = 2m;

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

    /// <summary>
    /// 升级只抬<b>基础抽数</b>（2 → 3）；翻转上限按 2026-10-15 delta 固定 <b>2</b>（升级不再抬上限）。
    /// </summary>
    protected override void OnUpgrade() => DynamicVars["Draw"].UpgradeValueBy(1m);
}
