using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Gap;
namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>缓收</b>（CardGAP2，v2）· <b>1 费 · Uncommon · 技能</b> —— 本回合<b>回归</b>时：若间隙里 ≥4 张牌，你不再丢弃当前手牌。
/// 升级：本回合回归时，你<b>抽 2 张牌</b>（保护仍在）。
/// </summary>
/// <remarks>
/// ① 效果：在间隙保护位上挂"门槛 4 张"；升级再叠一项"回归成功后抽 2 张"。
/// ② 差分：无差分。
/// ③ 依赖：时空裂隙 —— 保护位 <c>GapRecallGuard</c>（本回合限定，回合/战斗一变自动失效），生效点是 <c>GapRecallActions.Recall</c>。
/// ④ ⚠️ 升级口径按"保护 + 补抽"实现（机制层的 API 就是 <c>Arm</c> + <c>ArmDrawOnReturn</c> 两段）；
///    卡表若只要"抽 2 张"不要保护，改 <c>OnPlay</c> 里那一行即可。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSlowRecall() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Uncommon))
{
    /// <summary>间隙里至少几张才跳过"丢手牌"（卡表写死的 4）。</summary>
    private const int GapThreshold = 4;

    /// <summary>升级档回归后再抽几张。</summary>
    private const int DrawOnReturn = 2;

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        GapRecallGuard.Arm(Owner, GapThreshold);
        if (IsUpgraded)
        {
            GapRecallGuard.ArmDrawOnReturn(Owner, DrawOnReturn);
        }

        return Task.CompletedTask;
    }

}
