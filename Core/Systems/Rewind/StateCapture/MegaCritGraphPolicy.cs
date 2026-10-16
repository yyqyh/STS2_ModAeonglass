using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2_Aeonglass.Core.Systems.Rewind.Graph;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 本体策略：把"身份引用"从引擎对象扩展到模型 / 实体 / 战斗态 / 拍堆。
/// </summary>
/// <remarks>
/// ① 职责：<c>IGraphPolicy</c> 的本体实现。
/// ② 调用时机：字段图抓取/恢复时判定原子与保留引用。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal sealed class MegaCritGraphPolicy : GraphPolicies
{
    /// <summary>全局单例：策略无状态，缓存字段表，复用即可。</summary>
    internal static MegaCritGraphPolicy Instance { get; } = new();

    public override bool IsIdentityReference(object value) =>
        base.IsIdentityReference(value)
        || value is AbstractModel
            or ModelId
            or Player
            or Creature
            or CombatState
            or RunState
            or CardPile
            or Rng
            or DynamicVarSet;

    // ============================ 字段策略审计（2026-10-16，清掉旧 TODO）============================
    // 旧 TODO 问的是"Rng / CardEnergyCost / DynamicVarSet 要不要特例克隆"。逐个核对 0.111 源码后的结论：
    //
    // ① Rng（`Core/Random/Rng.cs`）：**改为按引用保留**。RNG 的状态由 `RngOps` 单独抓（run 的 Rng 集 + 每只怪的
    //    `MonsterModel.Rng` + 我们的 StsRng），图里若出现 Rng 只会拷出一份"脱离注册表的克隆"，写回去毫无意义。
    // ② CardEnergyCost（`Core/Entities/Cards/CardEnergyCost.cs`）：**不需要特例**。它是 `CardModel` 的字段，
    //    而**卡模型目前根本不进图**（`CombatStateSnapshot` 只抓 power 与遗物）⇒ 这条路现在走不到；真要抓卡，
    //    它的 `_card` 反查是 `CardModel`（= `AbstractModel`）已被按引用保留，其余是 `_base` / `_localModifiers`
    //    这类值字段，默认克隆即可。
    // ③ DynamicVarSet（`Core/Localization/DynamicVars/DynamicVarSet.cs`）：**保持按引用保留**（= 不还原其内部数值）。
    //    power 的层数以 `PowerModel._amount` 为准、那张字段已在图里（恢复后自比对也校验它，见
    //    `CombatStateSnapshot.CollectVerifyProblems`）⇒ 保持现状；将来若出现"数值只放在 DynamicVars 里"的能力，
    //    再单独给它做特例克隆。
}
