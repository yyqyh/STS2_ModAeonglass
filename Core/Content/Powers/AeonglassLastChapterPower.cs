using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「时之终章」挂着的能力：每回合开始时额外抽 5 张、额外获得 3 点能量。</summary>
/// <remarks>
/// <para>
/// ① 两个接缝都是本体现成的：
/// 抽牌 = 覆写 <c>ModifyHandDraw</c>（本体 <c>BagOfPreparation</c> 同款，调用点 <c>CombatManager.cs:905</c>）；
/// 能量 = 覆写 <c>AfterEnergyReset</c> 里 <c>PlayerCmd.GainEnergy</c>（本体 <c>ArtOfWar</c> 同款，调用点 <c>CombatManager.cs:901</c>）。
/// </para>
/// <para>
/// ② 次数都是常量、不读 <c>Amount</c> ⇒ 重复打出只刷新，不会叠加成 +10/+6。
/// </para>
/// <para>
/// ③ 手牌上限由本体在抽牌时自己截断（<c>CardPile.MaxCardsInHand</c>）⇒ 这里不额外处理。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassLastChapterPower : ModPowerTemplate
{
    /// <summary>每回合额外抽几张。</summary>
    private const decimal ExtraDrawPerTurn = 5m;

    /// <summary>每回合额外获得几点能量。</summary>
    private const decimal ExtraEnergyPerTurn = 3m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // ⚠️ 能力的 Owner 是 Creature（不是 Player）⇒ 比的是 player.Creature。
    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player.Creature == Owner ? count + ExtraDrawPerTurn : count;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        await PlayerCmd.GainEnergy(ExtraEnergyPerTurn, player);
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 时之终章：回合开始 额外 +{ExtraEnergyPerTurn} 能量 / +{ExtraDrawPerTurn} 抽");
    }
}
