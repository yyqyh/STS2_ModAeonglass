using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>「跳过你的下一个回合」</b>（豪赌的代价）：下一个回合<b>什么都不发生</b> —— 不抽牌、不给能量、立刻结束。
/// </summary>
/// <remarks>
/// <para>
/// <b>怎么做到"什么都不发生"</b>：不补丁、不拦回合管线，而是挂在本体已有的三个钩子上，各堵一处：
/// ① <see cref="ModifyHandDraw" /> 返回 0 ⇒ 那一次**手牌抽取**不发生；
/// ② <see cref="AfterEnergyReset" /> 把刚重置出来的能量收干 ⇒ 那回合**没有能量**；
/// ③ <see cref="AfterPlayerTurnStartEarly" /> 里延迟一下直接<b>结束该回合</b>（<c>SetReadyToEndTurn</c>）并自毁。
/// </para>
/// <para>
/// ⚠️ 延迟那一小段是**故意的**：这个钩子跑在"回合开始"的钩子遍历里，
/// 立刻结束回合会和引擎自己的回合推进抢时序 ⇒ 等一拍再按"结束回合"。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassSkipNextTurnPower : ModPowerTemplate
{
    /// <summary>负面（红框）：它是代价。</summary>
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>结束回合前等多久（秒）—— 只为错开"回合开始的钩子遍历"。</summary>
    private const float EndTurnDelay = 0.25f;

    /// <summary>① 那一次手牌抽取 = 0 张。</summary>
    public override decimal ModifyHandDraw(Player player, decimal count)
        => ReferenceEquals(player.Creature, Owner) ? 0m : count;

    /// <summary>② 把刚重置出来的能量收干（"不给能量"）。</summary>
    public override async Task AfterEnergyReset(Player player)
    {
        if (!ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        var energy = player.PlayerCombatState?.Energy ?? 0;
        if (energy > 0)
        {
            await PlayerCmd.LoseEnergy(energy, player);
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 跳过回合：收干能量 {energy}，本回合不抽牌");
    }

    /// <summary>③ 本回合直接走完 + 自毁。</summary>
    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (!ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        var left = Amount;
        await PowerCmd.Apply<AeonglassSkipNextTurnPower>(choiceContext, Owner, -left, Owner, null);

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 跳过回合：本回合直接结束（-{left}）");
        _ = TaskHelper.RunSafely(EndTurnSoonAsync(player));
    }

    private static async Task EndTurnSoonAsync(Player player)
    {
        try
        {
            await Cmd.Wait(EndTurnDelay, ignoreCombatEnd: true);
            CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Host} 跳过回合：结束回合失败（{ex.GetType().Name}: {ex.Message}）");
        }
    }
}
