using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Stasis;
namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary><b>压舱</b>（STA-2 挂的能力）：每当你搁浅一张牌：获得 <c>Amount</c> 点格挡（**每回合 1 次**）。</summary>
[RegisterPower]
public sealed class AeonglassBallastPower : ModPowerTemplate, IStasisReturnListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _usedThisTurn;

    public async Task OnCardStranded(Player player, CardModel card, int combatsLeft)
    {
        if (_usedThisTurn || Amount <= 0m || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        _usedThisTurn = true;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 压舱：{card.Id.Entry} 被搁浅 ⇒ +{Amount} 格挡");
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, null);
    }

    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (ReferenceEquals(player.Creature, Owner))
        {
            _usedThisTurn = false;
        }

        return Task.CompletedTask;
    }
}
