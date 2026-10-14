using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>相位律</b>（PH-7 挂的能力）：每回合开始时按相位给收益 ——
/// 沙砾侧 +格挡 / 平衡档 +格挡 / 空隙侧抽 1；<b>消逝副</b>改为看"上回合有没有消耗过沙砾"。
/// </summary>
/// <remarks>
/// <c>Amount</c> = 沙砾侧每回合的格挡（升级 +1）；平衡档固定 1 挡。
/// </remarks>
[RegisterPower]
public sealed class AeonglassPhaseLawPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>平衡档每回合的格挡（定值）。</summary>
    private const decimal BalanceBlock = 1m;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (!ReferenceEquals(player.Creature, Owner) || Amount <= 0m)
        {
            return;
        }

        var side = DeckVariants.SideOf(player, player.Deck.Cards.FirstOrDefault()!);
        _ = side; // 相位律的判定与"牌在哪副"无关；下面按玩家所在副的差分口径走。

        var spentLastTurn = SandTurnCounters.SandsSpentLastTurn(player);
        var phase = SandPhases.Of(player);

        // 消逝副：只看"上回合消耗过沙砾"；其余（永恒副 / 共用）：按相位三档。
        var useVariant = VariantSideOf(player) == DeckSide.B;

        if (useVariant)
        {
            if (spentLastTurn > 0)
            {
                await CardPileCmd.Draw(choiceContext, 1, player);
                Const.Logger.Info($"[{Const.ModId}] 相位律（消逝副）：上回合消耗过沙砾 ⇒ 抽 1");
            }
            else
            {
                await CreatureCmd.GainBlock(player.Creature, BalanceBlock, ValueProp.Move, null);
            }

            return;
        }

        switch (phase)
        {
            case SandPhase.Sand:
                await CreatureCmd.GainBlock(player.Creature, Amount, ValueProp.Move, null);
                break;
            case SandPhase.Balanced:
                await CreatureCmd.GainBlock(player.Creature, BalanceBlock, ValueProp.Move, null);
                break;
            default:
                await CardPileCmd.Draw(choiceContext, 1, player);
                break;
        }
    }

    /// <summary>当前生效副（拿不到牌时按 A 副算）。</summary>
    private static DeckSide VariantSideOf(Player player)
    {
        var probe = player.Deck.Cards.FirstOrDefault();
        return probe is null ? DeckSide.A : DeckVariants.SideOf(player, probe);
    }
}
