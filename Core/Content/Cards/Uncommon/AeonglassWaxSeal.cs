using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Relics;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>蜡封</b>（NEW-42，2026-10-20）· <b>2 费 · Uncommon · 能力 · 搁浅 3</b> ——
/// 战斗结束后获得一件蜡质遗物。升级：费用 1。
/// </summary>
/// <remarks>
/// ① 待办记在初始遗物上（跨战斗/读档），战斗结束按 ToyBox 同一条链路发蜡质遗物奖励。
/// ② 本卡自己搁浅 3 场（能力卡也会进搁浅区，不常驻能力栏）。
/// ③ 依赖：<c>AeonglassCore.QueueWaxSeal</c> + <c>AfterCombatEnd</c>。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassWaxSeal()
    : AeonglassStasisCardTemplate(
        AeonglassCardSpec.Power(2, CardRarity.Uncommon)
            .UpgradeEnergy(-1),
        strandCombats: 3)
{
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var ok = AeonglassCore.QueueWaxSeal(Owner);
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 蜡封：战斗结束待办 +1（成功={ok}）");
        return Task.CompletedTask;
    }
}
