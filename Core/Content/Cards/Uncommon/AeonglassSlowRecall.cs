using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>缓收</b>（CardGAP2）：1 费 · 本回合回归时：若间隙里 ≥4 张牌，你不再丢弃当前手牌。
/// （升级：回归后再抽 2 张。）
/// </summary>
/// <remarks>保护位挂在 <see cref="GapRecallGuard" /> 上，真正生效点在 <c>TimeGap.Recall</c>（它才数间隙）。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSlowRecall() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    /// <summary>门槛（卡表定值）。</summary>
    private const int GapThreshold = 4;

    /// <summary>升级后回归完再抽几张。</summary>
    private const int BaseDrawOnReturn = 0;

    private const int UpgradedDrawOnReturn = 2;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        GapRecallGuard.Arm(Owner, GapThreshold);
        if (IsUpgraded)
        {
            GapRecallGuard.ArmDrawOnReturn(Owner, UpgradedDrawOnReturn);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        // 升级档的额外收益由 ArmDrawOnReturn 承担（见 OnPlay）——这里不需要改数值，留在方法里做占位说明。
        _ = BaseDrawOnReturn;
    }
}
