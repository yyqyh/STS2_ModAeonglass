using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary><b>惯性</b>（INV-3 挂的能力）：每当你翻转沙漏：抽 <c>Amount</c> 张牌（**每回合 1 次**）。</summary>
[RegisterPower]
public sealed class AeonglassInertiaPower : ModPowerTemplate, ISandInvertListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>本回合是否已经触发过（"每回合 1 次"的闸）。</summary>
    private bool _usedThisTurn;

    /// <summary>本回合翻转到第几次时清的闸（用来识别"新回合"）。</summary>
    private int _lastInvertCount;

    public async Task OnSandInverted(PlayerChoiceContext choiceContext, Player player, int invertsThisTurn)
    {
        // 次数回到 1 ⇒ 说明进入了新回合，闸门重置（不需要额外的回合钩子）。
        if (invertsThisTurn <= 1)
        {
            _usedThisTurn = false;
        }

        _lastInvertCount = invertsThisTurn;

        if (_usedThisTurn || Amount <= 0m || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        _usedThisTurn = true;
        Const.Logger.Info($"[{Const.ModId}] 惯性：翻转触发 ⇒ 抽 {Amount} 张");
        await CardPileCmd.Draw(choiceContext, (int)Amount, player);
    }
}
