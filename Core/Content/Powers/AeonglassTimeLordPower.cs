using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「时间主宰」挂着的能力：本场战斗中塞凋萎一律无效（纯闸门，无主动效果）。</summary>
/// <remarks>
/// ① 职责：给 <c>WitherEffects.Inject</c> 一个"本场已在场"的判据。
/// ② 挂点与时机：卡打出时 PowerCmd.Apply；战斗结束随能力一起清（本体战斗级能力自动清）。
/// ③ 依赖：<c>WitherEffects.Inject</c>（唯一收口点）。
/// ④ 不叠层：Amount 不参与判定（再打一张只是刷新）。
/// </remarks>
[RegisterPower]
public sealed class AeonglassTimeLordPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>这个玩家身上有没有"本场不再被塞凋萎"的能力（给 <c>WitherEffects.Inject</c> 用）。</summary>
    internal static bool BlocksWithers(Player? player) =>
        player?.Creature.Powers.OfType<AeonglassTimeLordPower>().Any() == true;
}
