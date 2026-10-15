using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>高维观测</b>（INT-6 挂的能力，差分）：每回合开始时，若你<b>上回合操作过意图</b>（推迟 / 提前 / 剥夺），
/// 抽 <c>Amount</c> 张；<b>消逝副</b>改为获得等量能量。
/// </summary>
/// <remarks>
/// 副在**打出那一刻**由卡写好（<see cref="EnergyVariant" />）—— 不读"现在在哪一副"，
/// 因为能力会跨副存留，而这张牌的效果应该在打出的瞬间定型。
/// </remarks>
[RegisterPower]
public sealed class AeonglassHighObservePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>true = 消逝副（改为获得能量）；false = 永恒副（抽牌）。</summary>
    public bool EnergyVariant { get; set; }

    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (Amount <= 0m || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        var manipulated = CombatHistory.IntentManipulationsLastTurn(player);
        if (manipulated <= 0)
        {
            return;
        }

        if (EnergyVariant)
        {
            await PlayerCmd.GainEnergy(Amount, player);
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 高维观测（消逝副）：上回合操作过 {manipulated} 次意图 ⇒ +{Amount} 能量");
            return;
        }

        await CardPileCmd.Draw(choiceContext, (int)Amount, player);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 高维观测（永恒副）：上回合操作过 {manipulated} 次意图 ⇒ 抽 {Amount}");
    }
}
