using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary><b>摆钟</b>（UTL-4 挂的能力）：每回合开始时抽 <c>Amount</c> 张、然后弃 <c>Amount</c> 张。</summary>
/// <remarks>
/// 弃牌用**随机弃**（`CardPileCmd.DiscardRandom`）—— 这是能力牌的"每回合白拿"代价，
/// 若让玩家每回合都手动选，会拖慢节奏；卡表口径是"靠弃牌压住收益"，随机弃满足这一点。
/// </remarks>
[RegisterPower]
public sealed class AeonglassPendulumPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (!ReferenceEquals(player.Creature, Owner) || Amount <= 0m)
        {
            return;
        }

        var count = (int)Amount;
        await CardPileCmd.Draw(choiceContext, count, player);
        await DiscardRandomAsync(choiceContext, player, count);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 摆钟：抽 {count} 弃 {count}");
    }

    /// <summary>随机弃若干张（用跑局自带的洗牌随机流 ⇒ 可复现、被回档快照覆盖）。</summary>
    private static async Task DiscardRandomAsync(
        PlayerChoiceContext choiceContext,
        Player player,
        int count)
    {
        var hand = player.PlayerCombatState?.Hand.Cards.ToList() ?? [];
        for (var i = 0; i < count && hand.Count > 0; i++)
        {
            var index = player.RunState.Rng.Shuffle.NextInt(hand.Count);
            var card = hand[index];
            hand.RemoveAt(index);
            await CardCmd.Discard(choiceContext, card);
        }
    }
}
