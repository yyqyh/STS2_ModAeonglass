using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Presentation.Visuals;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>**M2 入口卡「坍缩」**：打出即坍缩（手牌 → 时间裂隙）。</summary>
/// <remarks>
/// <para>
/// 0 费技能。作用与战斗面板 / 战斗按键**完全同一条路**：<see cref="GapSequence.CollapseAsync" /> → <c>TimeGap.Activate</c>，
/// 所以两道闸（每回合 1 次 + 必须交替）与 2 沙砾的代价都由机制侧统一判定。
/// </para>
/// <para>
/// ⚠️ 这是**测试卡**（数值/费用/稀有度都由卡表定稿时替换）；放在角色卡池里只是为了能在奖励 / 商店里拿到它做实测。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCollapse : ModCardTemplate
{
    public AeonglassCollapse() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var result = await GapSequence.CollapseAsync(Owner);
        Const.Logger.Info(
            $"[{Const.ModId}] [M2] 卡「坍缩」：{(result.Ok ? "成功" : "被拒")} — {result.Message}");
    }
}
