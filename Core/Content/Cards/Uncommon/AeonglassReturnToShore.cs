using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>归岸</b>（NEW-43，2026-10-20）· <b>0 费 · Uncommon · 技能 · 搁浅 1</b> ——
/// 恢复 7 点生命（升级 10）；打出后搁浅战斗数 +1。
/// </summary>
/// <remarks>
/// ① "打出后搁浅战斗数 +1"写在<b>母本</b>的 <c>StrandLevel</c> 上：本次仍按旧值搁浅，
///    下一次回来后的每一轮都多 1（跨战斗/读档保留）。
/// ② 依赖：搁浅基类的 <c>AfterSentToStasis</c> 钩子 + <c>CreatureCmd.Heal</c>。
/// ③ ⚠️ 战斗副本不保存成长；成长只认牌组里的母本。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassReturnToShore()
    : AeonglassStasisCardTemplate(
        AeonglassCardSpec.Skill(0, CardRarity.Uncommon)
            .Var("Heal", 7m, upgradeDelta: 3m),
        strandCombats: 1)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var heal = DynamicVars["Heal"].BaseValue;
        await CreatureCmd.Heal(Owner.Creature, heal);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 归岸：恢复 {heal} 点生命");
    }

    protected override void AfterSentToStasis(CardModel master)
    {
        if (master is AeonglassReturnToShore shore)
        {
            shore.StrandLevel++;
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Stasis} 归岸：下次搁浅提升为 {shore.StrandLevel} 场");
        }
    }
}
