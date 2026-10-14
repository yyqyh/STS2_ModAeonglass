using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「蓄隙」（GAP-5 挂的能力）：每回合开始时，若间隙里 ≥3 张牌：获得 <c>Amount</c> 点格挡。</summary>
[RegisterPower]
public sealed class AeonglassGapPulsePower : ModPowerTemplate
{
    /// <summary>门槛（卡表定值 3）。</summary>
    private const int GapThreshold = 3;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0m || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        var gap = TimeGap.CountOf(player);
        if (gap < GapThreshold)
        {
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] 蓄隙：间隙 {gap} 张 ⇒ +{Amount} 格挡");
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, null);
    }
}
