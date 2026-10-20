using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>双源取牌</b>（能力本体）：常规抽牌 −N，回合开始从弃牌堆补抽 N 张。
/// </summary>
/// <remarks>
/// ① <see cref="ModifyHandDraw" /> 只改"从抽牌堆抽"的数量；弃牌堆那一段在
///    <see cref="AfterPlayerTurnStartEarly" />（常规手牌抽完之后）补。
/// ② <c>Amount</c> = 从弃牌堆抽几张（2 / 3）；叠层按总量生效。
/// ③ 弃牌堆不足按 <c>DiscardDraw</c> 的实际张数截断。
/// </remarks>
[RegisterPower]
public sealed class AeonglassDoubleSourcePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        ReferenceEquals(player, Owner.Player) ? Math.Max(0m, count - Amount) : count;

    public override async Task AfterPlayerTurnStartEarly(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (!ReferenceEquals(player, Owner.Player) || Amount <= 0m)
        {
            return;
        }

        await DiscardDraw.DrawAsync(choiceContext, player, (int)Amount);
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        Task.CompletedTask;
}
