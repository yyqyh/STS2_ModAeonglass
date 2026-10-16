using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2_Aeonglass.Core.Systems.Rewind.Graph;
using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>战斗快照的<b>恢复后自比对</b>分册（2026-10-16 补）。</summary>
/// <remarks>
/// ① 职责：CollectVerifyProblems —— power 层数 / 生物 hp·block / 怪状态机 三项自比对。
/// ② 挂点与时机：TryRestore 的最后一步；命中即返回 false，让事务走既有回滚。
/// ③ 依赖：快照的 _models（字段图，含 TryGetCaptured）、_allies/_enemies、Coordinates。
/// ④ ⚠️ 卡模型不在快照范围 ⇒ 不校验卡级字段（见体检报告 §8 的已知边界）。
/// </remarks>
internal sealed partial class CombatStateSnapshot
{
    /// <summary>
    /// 恢复后的<b>自比对</b>（2026-10-16 补）：拿"快照抓到的值"与"恢复后的运行时读值"比一遍。
    /// </summary>
    /// <remarks>
    /// <para>校验范围只覆盖**快照真的存过的东西**：power 层数（<c>PowerModel._amount</c>，在图里）、
    /// 生物 hp / block（<c>Vitals</c>）、怪的状态机内部状态（结构自检）、
    /// 以及 2026-10-16 起新增的**卡级字段**（升级等级 / 解析后的耗能 / 附魔层数）。命中即让 <see cref="TryRestore" /> 返回 false
    /// ⇒ 事务走既有的回滚路径（`RewindTransaction.Rollback`）。</para>
    /// <para>⚠️ <b>已知边界（收窄后）</b>：`DynamicVarSet` 仍按引用保留 ⇒ 卡面上的"伤害/格挡数值"不单独校验，
    /// 靠恢复后的一次重算 + UI 刷新补齐；若某类战斗内改值回不去，再单开一轮给它做特例克隆（见体检报告 §8）。</para>
    /// </remarks>
    private List<string> CollectVerifyProblems()
    {
        var problems = new List<string>();

        // ① power 层数：图里抓到的 `_amount` vs 现在的 Amount
        foreach (var entry in _models.Where(e => e.Kind == PowerKind))
        {
            var power = (PowerModel)entry.Model;
            if (!entry.Graph.TryGetCaptured(Coordinates.PowerModel.Amount, out var captured) || captured is not int expected)
            {
                continue;
            }

            if (power.Amount != expected)
            {
                problems.Add($"power {power.Id.Entry}: amount={power.Amount} 期望 {expected}");
            }
        }

        // ② 我方生物 hp / block（按开启的 scope 分别比）
        foreach (var (creature, vitals) in _allies)
        {
            if (_scope.RestorePlayerHp && creature.CurrentHp != vitals.Hp)
            {
                problems.Add($"{creature.LogName}: hp={creature.CurrentHp} 期望 {vitals.Hp}");
            }

            if (_scope.RestorePlayerBlock && creature.Block != vitals.Block)
            {
                problems.Add($"{creature.LogName}: block={creature.Block} 期望 {vitals.Block}");
            }
        }

        // ②' 敌方生物
        foreach (var (creature, vitals) in _enemies)
        {
            if (_scope.RestoreEnemyHp && creature.CurrentHp != vitals.Hp)
            {
                problems.Add($"{creature.LogName}: hp={creature.CurrentHp} 期望 {vitals.Hp}");
            }

            if (_scope.RestoreEnemyBlock && creature.Block != vitals.Block)
            {
                problems.Add($"{creature.LogName}: block={creature.Block} 期望 {vitals.Block}");
            }
        }

        // ③ 怪的状态机：`_currentState` 必须存在、且能命中 `States`（防止复原到不存在的招式）
        if (_scope.RestoreTurnCounters && CombatAccess.State is { } state)
        {
            foreach (var creature in state.Creatures)
            {
                if (creature.Monster is not { } monster || monster.MoveStateMachine is not { } machine)
                {
                    continue;
                }

                if (!Reflect.TryGet<MonsterState>(machine, Coordinates.MonsterMoveStateMachine.CurrentState, out var current)
                    || current is null)
                {
                    problems.Add($"monster {monster.Id.Entry}: 状态机没有 _currentState");
                    continue;
                }

                if (!machine.States.ContainsKey(current.Id))
                {
                    problems.Add($"monster {monster.Id.Entry}: _currentState 「{current.Id}」不在 States 里");
                }
            }
        }

        // ④ 卡级字段（2026-10-16 · B4）：升级等级 / 解析后的耗能 / 附魔层数。
        //    - 等级：`_currentUpgradeLevel` 是本体手写字段（不是自动属性）⇒ 按名取快照值。
        //    - 耗能：快照里存的是**克隆出来的 cost 对象**（它的 `_card` 按引用保留 ⇒ 能自己算出解析值），
        //      与当前对象的解析值对比即可；`_energyCost` 可能为 null（懒建）⇒ 取不到就跳过。
        //    - 附魔层数：附魔模型自己是独立条目，直接比它的 `_amount`。
        foreach (var entry in _models.Where(e => e.Kind == CardKind))
        {
            if (entry.Model is not CardModel card)
            {
                continue;
            }

            if (entry.Graph.TryGetCaptured("_currentUpgradeLevel", out var capturedLevel)
                && capturedLevel is int expectedLevel
                && card.CurrentUpgradeLevel != expectedLevel)
            {
                problems.Add($"card {card.Id.Entry}: 等级={card.CurrentUpgradeLevel} 期望 {expectedLevel}");
            }

            if (entry.Graph.TryGetCaptured("_energyCost", out var capturedCost)
                && capturedCost is CardEnergyCost expectedCost)
            {
                var actual = card.EnergyCost.GetWithModifiers(CostModifiers.Local);
                var expected = expectedCost.GetWithModifiers(CostModifiers.Local);
                if (actual != expected)
                {
                    problems.Add($"card {card.Id.Entry}: 耗能={actual} 期望 {expected}");
                }
            }
        }

        foreach (var entry in _models.Where(e => e.Kind == EnchantmentKind))
        {
            if (entry.Model is not EnchantmentModel enchantment
                || !entry.Graph.TryGetCaptured("_amount", out var capturedAmount)
                || capturedAmount is not int expectedAmount)
            {
                continue;
            }

            if (enchantment.Amount != expectedAmount)
            {
                problems.Add($"enchantment {enchantment.Id.Entry}: 层数={enchantment.Amount} 期望 {expectedAmount}");
            }
        }

        return problems;
    }
}
