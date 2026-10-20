using MegaCrit.Sts2.Core.Combat;
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
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>沉底淘牌</b>（能力本体）：回合开始时处理抽牌堆底部 N 张。
/// </summary>
/// <remarks>
/// ① 时机取 <see cref="BeforeHandDraw" />（常规抽牌之前），这样底部牌不会先被抽走。
/// ② 分流：迟滞 ⇒ 自动打出；状态 ⇒ 消耗；其余 ⇒ 弃牌堆。
/// ③ <c>Amount</c> = 处理张数（3 / 4）；叠层按总量生效。
/// </remarks>
[RegisterPower]
public sealed class AeonglassDredgePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (!ReferenceEquals(player, Owner.Player)
            || Amount <= 0m
            || player.PlayerCombatState is not { } piles)
        {
            return;
        }

        var draw = piles.DrawPile;
        var take = Math.Min((int)Amount, draw.Cards.Count);
        if (take <= 0)
        {
            return;
        }

        var bottom = draw.Cards.Skip(draw.Cards.Count - take).ToList();
        foreach (var card in bottom)
        {
            try
            {
                if (Hysteresis.IsStuck(card))
                {
                    await CardPileCmd.Add(card, PileType.Play);
                    await CardCmd.AutoPlay(choiceContext, card, null);
                    continue;
                }

                await CardPileCmd.Add(
                    card,
                    card.Type == CardType.Status ? PileType.Exhaust : PileType.Discard);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Host} 沉底淘牌：处理 {card.Id.Entry} 失败（忽略）："
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 沉底淘牌：处理抽牌堆底部 {bottom.Count} 张");
    }

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        Task.CompletedTask;
}
