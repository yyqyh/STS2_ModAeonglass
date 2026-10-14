using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary><b>齿车</b>（UTL-5 挂的能力）：每当你打出一张 <b>0 费</b>牌：获得 <c>Amount</c> 点能量（**每回合 1 次**）。</summary>
[RegisterPower]
public sealed class AeonglassCogwheelPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _usedThisTurn;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_usedThisTurn || Amount <= 0m || cardPlay.Card.Owner is not { } owner)
        {
            return;
        }

        if (!ReferenceEquals(owner.Creature, Owner) || cardPlay.Card.EnergyCost.Canonical != 0)
        {
            return;
        }

        _usedThisTurn = true;
        await PlayerCmd.GainEnergy(Amount, owner);
        Const.Logger.Info($"[{Const.ModId}] 齿车：打出 0 费牌 {cardPlay.Card.Id.Entry} ⇒ +{Amount} 能量");
    }

    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (ReferenceEquals(player.Creature, Owner))
        {
            _usedThisTurn = false;
        }

        return Task.CompletedTask;
    }
}
