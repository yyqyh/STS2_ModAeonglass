using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Stasis;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>无锚漂流</b>（能力本体）：玩家回合开始随机生成一张搁浅牌。
/// </summary>
/// <remarks>
/// ① 随机池 = 本模组实现 <see cref="IStasisCard" /> 的可获取卡；按 <c>CombatCardSelection</c> RNG 取一张。
/// ② 手牌满时生成牌进弃牌堆（不丢牌）。
/// ③ <c>Amount</c> 为层数，每层生成一张。
/// </remarks>
[RegisterPower]
public sealed class AeonglassDriftwoodPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private static List<CardModel>? _pool;

    private static List<CardModel> Pool => _pool ??= ModelDb.AllCards
        .Where(card => card.GetType().Assembly == typeof(AeonglassDriftwoodPower).Assembly)
        .Where(card => card is IStasisCard)
        .Where(card => card.Rarity is not CardRarity.Token and not CardRarity.Curse)
        .Where(card => card.Type is not CardType.Status and not CardType.Curse)
        .ToList();

    public override async Task AfterPlayerTurnStartEarly(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (!ReferenceEquals(player, Owner.Player)
            || Amount <= 0m
            || player.Creature.CombatState is not { } combat
            || player.PlayerCombatState is not { } piles)
        {
            return;
        }

        var pool = Pool;
        if (pool.Count == 0)
        {
            return;
        }

        var count = Math.Min((int)Amount, 3);
        for (var i = 0; i < count; i++)
        {
            var pick = pool[player.RunState.Rng.CombatCardSelection.NextInt(pool.Count)];
            var card = combat.CreateCard(pick, player);
            var pile = piles.Hand.Cards.Count >= CardPile.MaxCardsInHand
                ? PileType.Discard
                : PileType.Hand;
            await CardPileCmd.AddGeneratedCardToCombat(card, pile, player);
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Stasis} 无锚漂流：随机生成 {card.Id.Entry} → {pile}");
        }
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        Task.CompletedTask;
}
