using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>冻结形态</b>（NEW-4）的标记能力：在场期间，<c>FrozenFormRecorder</c> 会把你打出的其他能力
/// 记进 run 持久隐藏牌堆，留到下一场战斗开始时自动打出。
/// </summary>
/// <remarks>
/// 能力本身没有每回合效果；用 <see cref="PowerStackType.Single" />（重复打出不会叠层、只刷新同一份）。
/// </remarks>
[RegisterPower]
public sealed class AeonglassFrozenFormPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;
}
