using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>贷时</b>（ETR-3 挂的能力）：<b>把下回合的能量提前到本回合使用</b> —— 本回合 +、下回合对应 −。
/// </summary>
/// <remarks>
/// <para>
/// <b>借多少</b>（v2 二次更新：原卡面没写点数上限 ⇒ 按"提前用下回合的能量、下回合对应减少"实现）：
/// 每回合开始时先还上一回合的账，再按<b>你这个回合的能量上限</b>整份借出来 —— 不是固定 1 点。
/// 于是打出后的第一个完整回合会拿到一个"双倍能量"的大回合；之后每回合借 M 还 M，长线净值 0。
/// 要改成"玩家自选借多少"就在这里加一次询问（只动这一个方法）。
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
/// <para><c>Amount</c> 只用于图标显示（保留为 1），真正的借量每回合按当时的能量上限算。</para>
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
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 贷时：还上一回合的 {owed} 点能量");
        }

        // 借 = 你这个回合的能量上限（"不限点数上限" ⇒ 不是固定 1 点，而是把下个回合的整份提前用）。
        var amount = Math.Max(0, player.PlayerCombatState?.MaxEnergy ?? 0);
        if (amount <= 0)
        {
            return;
        }

        await PlayerCmd.GainEnergy(amount, player);
        _pendingDebt = amount;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 贷时：把下回合的 {amount} 点能量提前到本回合（下回合 −{amount}）");
    }
}
