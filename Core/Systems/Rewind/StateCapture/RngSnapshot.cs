using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 随机数快照：各条流的计数值。
/// </summary>
/// <remarks>
/// ① 职责：数据载体。
/// ② 调用时机：<c>RngOps</c> 存取。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal sealed class RngSnapshot
{
    internal SerializableRunRngSet? RunRng { get; init; }

    /// <summary>按生物引用索引（<see cref="ReferenceEqualityComparer"/>：不能用值比较）。</summary>
    internal Dictionary<Creature, SerializableRng> MonsterRng { get; } =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>mod 自己的 StsRng，按 <c>ModRunRngRegistry</c> 的 streamId 索引。</summary>
    internal Dictionary<string, SerializableRng> ModRng { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    internal int EntryCount => (RunRng is null ? 0 : 1) + MonsterRng.Count + ModRng.Count;
}
