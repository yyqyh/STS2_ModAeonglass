using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>贷时</b>（ETR-3 挂的能力）：每回合开始时借 <c>Amount</c> 点能量 —— 本回合 +、下回合 −。
/// </summary>
/// <remarks>
/// <para>
/// <b>「你可以借」实现成「每回合自动借」</b>（与 SD 通道的「可消耗 N 沙砾 = 够就花」同口径）：
/// 借贷长线上净值 0（借的那点下回合要还），自动借不会让玩家吃亏，却省掉每回合一次确认弹窗。
/// 要改成「玩家可选」只需在这里加一次询问。
/// </para>
/// <para>
/// <b>账记在自己身上</b>（<see cref="_pendingDebt" />）而不是另挂一个「透支」能力：挂账能力每次
/// <c>Apply</c> 会叠量，下一回合就会把两笔一起扣掉，净收益在 ±1 之间抖动；自己的回合钩子天然
/// 每回合只跑一次，读数稳定。
/// </para>
/// <para>
/// 顺序是「先还上一回合的账，再借新的」—— 能量在本体 <c>SetupPlayerTurn</c> 开头刚重置过，
/// 所以这次扣减一定落在本回合的池子里。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassLoanTimePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>下一回合要还的能量（本回合借的那笔）。</summary>
    private int _pendingDebt;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (!ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        if (_pendingDebt > 0)
        {
            var owed = _pendingDebt;
            _pendingDebt = 0;
            await PlayerCmd.LoseEnergy(owed, player);
            Const.Logger.Info($"[{Const.ModId}] 贷时：还上一回合的 {owed} 点能量");
        }

        if (Amount <= 0m)
        {
            return;
        }

        await PlayerCmd.GainEnergy(Amount, player);
        _pendingDebt = (int)Amount;
        Const.Logger.Info($"[{Const.ModId}] 贷时：借 {Amount} 点能量（下回合 −{Amount}）");
    }
}
