using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2_Aeonglass.Core.Systems.Rewind.Graph;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding;

/// <summary>
/// 本体策略：把"身份引用"从引擎对象扩展成模型 / 实体 / 战斗态 / 拍堆。
/// 这些对象不在这里克隆 —— 模型各自由字段快照单独负责（M2 接）。
/// </summary>
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

    // TODO(M2)：Rng / CardEnergyCost / DynamicVarSet 的特例克隆（先核对 0.111 构造签名）。
}
