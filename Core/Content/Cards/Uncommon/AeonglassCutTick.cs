using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>断刻</b>（新卡 2026-10-19）· <b>3 费 · Uncommon · 攻击</b> ——
/// 造成 32 点伤害（升级 48）；回合结束时获得[迟滞]，打出后失去[迟滞]。
/// </summary>
/// <remarks>
/// ① 效果：数值走模板；迟滞是**运行时标记**（<c>CutTickMarking</c> 在玩家回合结束给所有副本打标，
///    本卡打出时 <c>Hysteresis.Unmark</c> 清掉自己）。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：<c>AeonglassCore.AfterSideTurnEnd</c> → <c>CutTickMarking.MarkAll</c>。
/// ④ ⚠️ 运行时标记的已知限制：机制一定生效，但卡面不一定出「迟滞」词条（与揭幕同一条）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCutTick()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Attack(3, CardRarity.Uncommon, 32m, 16m))
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Hysteresis.Unmark(this);
        await Task.CompletedTask;
    }
}
