using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「编年」（ANN-4 挂的能力）：每回合开始时，若你**上回合打出的牌 ≤1 张**：获得 <c>Amount</c> 点格挡。</summary>
[RegisterPower]
public sealed class AeonglassAnnalsPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0m || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        var played = CombatHistory.CardsPlayedLastTurn(player);
        if (played > 1)
        {
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] 编年：上回合只打出 {played} 张 ⇒ +{Amount} 格挡");
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, null);
    }
}
