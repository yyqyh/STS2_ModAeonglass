using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
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
            or DynamicVarSet;

    // TODO(时空裂隙)：Rng / CardEnergyCost / DynamicVarSet 的特例克隆（先核对 0.111 构造签名）。
}
