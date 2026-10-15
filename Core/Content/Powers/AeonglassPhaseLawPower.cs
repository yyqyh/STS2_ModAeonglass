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
/// <b>相位律</b>（CardPH7 挂的能力，delta 20261015）：每回合开始时按相位三档给收益 ——
/// <b>沙砾侧 +1 能量 / 平衡档 3 点格挡 / 空隙侧抽 1 张牌</b>；
/// <b>消逝副</b>三档镜像换位（沙砾侧抽 1 / 平衡档 3 点格挡 / 空隙侧 +1 能量）。
/// </summary>
/// <remarks>
/// <para>
/// 2026-10-15 delta 把三档从"两档格挡 + 一档抽牌"改成"能量 / 格挡 / 抽牌"各占一档，且<b>升级不抬数值</b>
/// ⇒ 这里不再读 <c>Amount</c> 做缩放，<c>Amount</c> 只当"这张能力在场"的标记。
/// </para>
/// <para>
/// ⚠️ 沙砾侧那一档（+1 能量）是每回合白拿 1 费，卡池侧已标"极强引擎"；要削就改这一个常量。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassPhaseLawPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>沙砾侧 / 空隙侧那一档的能量。</summary>
    private const decimal EnergyPerTurn = 1m;

    /// <summary>平衡档每回合的格挡（两副相同）。</summary>
    private const decimal BalanceBlock = 3m;

    /// <summary>抽牌那一档抽几张。</summary>
    private const int DrawPerTurn = 1;

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (!ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        // 消逝副（B）：三档镜像换位；永时副（A）：沙砾侧给能量、空隙侧给抽牌。
        var mirrored = VariantSideOf(player) == DeckSide.B;
        var phase = SandPhases.Of(player);
        var what = string.Empty;

        switch (phase)
        {
            case SandPhase.Sand when !mirrored:
            case SandPhase.Void when mirrored:
                await PlayerCmd.GainEnergy(EnergyPerTurn, player);
                what = $"能量 +{EnergyPerTurn}";
                break;

            case SandPhase.Balanced:
                await CreatureCmd.GainBlock(player.Creature, BalanceBlock, ValueProp.Move, null);
                what = $"格挡 +{BalanceBlock}";
                break;

            default:
                await CardPileCmd.Draw(choiceContext, DrawPerTurn, player);
                what = $"抽 {DrawPerTurn}";
                break;
        }

        Const.Logger.Info($"[{Const.ModId}] 相位律（{(mirrored ? "消逝副" : "永时副")} · {phase}）：{what}");
    }

    /// <summary>当前生效副（拿不到牌时按 A 副算）。</summary>
    private static DeckSide VariantSideOf(Player player)
    {
        var probe = player.Deck.Cards.FirstOrDefault();
        return probe is null ? DeckSide.A : DeckVariants.SideOf(player, probe);
    }
}
