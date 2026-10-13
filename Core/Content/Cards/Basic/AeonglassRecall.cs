using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Presentation.Visuals;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>**M2 入口卡「唤回」**：打出即唤回（时间裂隙 → 手牌，替换当前手牌）。</summary>
/// <remarks>
/// 与「坍缩」同一条路（<see cref="GapSequence.RecallAsync" /> → <c>TimeGap.Recall</c>）：
/// 两道闸 + 统一 2 沙砾都由机制侧判定；失败时整次不动（不弃手牌）。
/// ⚠️ 同样是**测试卡**，数值由卡表定稿替换。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassRecall : ModCardTemplate
{
    public AeonglassRecall() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var result = await GapSequence.RecallAsync(Owner);
        Const.Logger.Info(
            $"[{Const.ModId}] [M2] 卡「唤回」：{(result.Ok ? "成功" : "被拒")} — {result.Message}");
    }
}
