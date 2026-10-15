using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;


namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 回溯的 RNG 快照（本 Lab 补齐的唯一规格缺口）。
/// 覆盖三处：
/// <list type="number">
///   <item><c>RunRngSet</c>（含全部 <c>RunRngType</c> 子流）—— <c>ToSerializable()</c> / <c>LoadFromSerializable()</c>；</item>
///   <item>每只怪的 <c>MonsterModel.Rng</c>（<c>CombatState.CreateCreature</c> 按 mapCoord+act+combatId 播种的那个）；</item>
///   <item>mod 自己的 <c>StsRng</c>（<c>ModRunRngRegistry</c> / <c>RitsuLibFramework.GetModRunRng</c>）。</item>
/// </list>
/// <b>一律"灌回原实例"</b>（<c>LoadFromSerializable</c>），不替换实例 —— 引擎里到处按引用持有这些 Rng。
/// </summary>
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
