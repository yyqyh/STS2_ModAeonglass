using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「宽幅」（GRW-1 挂的能力）：你的手牌上限 +<c>Amount</c>。</summary>
/// <remarks>升级档（手牌上限 +1）另带"每回合开始时抽 1 张"—— 由卡在打出时按"是否已升级"写下开关。</remarks>
[RegisterPower]
public sealed class AeonglassWideFramePower : AeonglassHandLimitPower
{
    /// <summary>升级档：每回合开始时额外抽 1 张。</summary>
    public bool DrawEachTurn { get; set; }

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (!DrawEachTurn || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        await CardPileCmd.Draw(choiceContext, 1, player);
    }
}

