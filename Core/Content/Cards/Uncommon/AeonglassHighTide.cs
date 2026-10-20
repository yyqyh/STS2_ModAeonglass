using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>涨潮</b>（NEW-44，2026-10-20）· <b>0 费 · Uncommon · 攻击 · 搁浅 1</b> ——
/// 对所有敌人造成 6 点伤害 4 次（升级 7 点 × 5 次）；打出后费用永久 +1。
/// </summary>
/// <remarks>
/// ① 多段 AOE 直接循环模板的伤害效果（每次重新读 <c>DamageThisPlay</c>）。
/// ② 费用永久 +1 写在<b>母本</b>上（<c>SetCustomBaseCost</c>），随卡牌存档走。
/// ③ 依赖：搁浅基类的 <c>AfterSentToStasis</c> 钩子 + <c>CardEffects.Damage</c> 的全体目标分支。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassHighTide()
    : AeonglassStasisCardTemplate(
        AeonglassCardSpec.Attack(
                0,
                CardRarity.Uncommon,
                6m,
                upgradeDelta: 1m,
                target: TargetType.AllEnemies)
            .Var("Hits", 4m, upgradeDelta: 1m),
        strandCombats: 1)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hits = (int)DynamicVars["Hits"].BaseValue;
        for (var i = 0; i < hits; i++)
        {
            await RunSpecEffectsAsync(choiceContext, cardPlay);
        }
    }

    protected override void AfterSentToStasis(CardModel master)
    {
        var current = master.EnergyCost.GetWithModifiers(CostModifiers.Local);
        master.EnergyCost.SetCustomBaseCost(current + 1);
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 涨潮：母本费用永久 {current} → {current + 1}");
    }
}
