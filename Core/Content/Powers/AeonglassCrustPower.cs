using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary><b>结壳</b>（SD-7 挂的能力）：每当你消耗至少 1 点沙砾：获得 <c>Amount</c> 点格挡（**每回合 1 次**）。</summary>
[RegisterPower]
public sealed class AeonglassCrustPower : ModPowerTemplate, ISandSpendListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _usedThisTurn;

    public async Task OnSandSpent(Player player, int amount)
    {
        if (_usedThisTurn || Amount <= 0m || amount <= 0 || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        _usedThisTurn = true;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Sand} 结壳：消耗 {amount} 沙砾 ⇒ +{Amount} 格挡");
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
