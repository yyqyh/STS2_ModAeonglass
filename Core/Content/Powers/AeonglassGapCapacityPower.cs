using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「间隙容量 +N」（GRW-2 深隙 / GRW-3 摆幅 的结构成长）。</summary>
/// <remarks>
/// <b>读取方式</b>：<c>TimeGap.Capacity</c> 会去该玩家的能力里找这个 Power 求和 ⇒
/// 不需要"加/减"记账，能力在场就生效、离场（战斗结束 / 被移除）就自动失效。
/// </remarks>
[RegisterPower]
public sealed class AeonglassGapCapacityPower : ModPowerTemplate, IGapCapacitySource, IGapCollapseListener
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>升级档「深隙」：坍塌（坍缩）时额外抽 1 张（由卡在打出时按"是否已升级"写下）。</summary>
    public bool DrawOnCollapse { get; set; }

    /// <inheritdoc />
    public int GapCapacityBonus => (int)Amount;

    /// <summary>坍缩时额外抽 1 张（只有被升级档写开的那一份会触发）。</summary>
    public async Task OnCollapsed(Player player, int copies)
    {
        if (!DrawOnCollapse || !ReferenceEquals(player.Creature, Owner))
        {
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] 深隙（升级档）：坍缩 {copies} 张 ⇒ 额外抽 1 张");
        await CardPileCmd.Draw(
            new MegaCrit.Sts2.Core.GameActions.Multiplayer.BlockingPlayerChoiceContext(), 1, player);
    }

    /// <summary>该玩家身上所有"间隙容量"能力给出的加值（没有就是 0）。</summary>
    public static int BonusOf(MegaCrit.Sts2.Core.Entities.Players.Player player)
        => player.Creature.Powers.OfType<IGapCapacitySource>().Sum(source => source.GapCapacityBonus);
}
