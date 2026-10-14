using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「下回合少 N 点能量」（ETR-2 透支的挂账）。</summary>
/// <remarks>
/// 卡是在**玩家自己的回合**里打的，所以下一次为该玩家触发的
/// <c>AfterPlayerTurnStartEarly</c> 就是"下一回合" ⇒ 在那里扣能量并自毁。
/// </remarks>
[RegisterPower]
public sealed class AeonglassEnergyDebtPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0m || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        var debt = Amount;
        Const.Logger.Info($"[{Const.ModId}] 透支：下回合扣 {debt} 点能量");
        await PlayerCmd.LoseEnergy(debt, player);
        await PowerCmd.Apply<AeonglassEnergyDebtPower>(choiceContext, Owner, -debt, Owner, null);
    }
}
