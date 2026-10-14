using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>时枢</b>（UTL-6 挂的能力）：每回合开始时获得 <c>Amount</c> 点能量；<b>代价 = 你的手牌上限 −1</b>。
/// </summary>
/// <remarks>
/// 每回合白拿 1 点能量是<b>长期打破零和</b>的（卡表 notes），所以代价必须真实且常驻 ——
/// 手牌上限的 −1 走基类的手牌上限口（<c>Amount</c> 让给能量，代价另覆写一条）。
/// </remarks>
[RegisterPower]
public sealed class AeonglassTimeHubPower : AeonglassHandLimitPower
{
    /// <summary>手牌上限的代价（卡表定值 −1）。</summary>
    private const int HandLimitPenalty = -1;

    /// <inheritdoc />
    protected override int HandLimitDelta => HandLimitPenalty;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0m || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        await PlayerCmd.GainEnergy(Amount, player);
        Const.Logger.Info($"[{Const.ModId}] 时枢：每回合 +{Amount} 能量（手牌上限 {HandLimitPenalty}）");
    }
}
