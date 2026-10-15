using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 恢复后的<b>不变量校验</b>：牌归属 / 战斗牌登记 / 重复落堆 / 生物 id。
/// </summary>
/// <remarks>
/// ① 职责：校验并回报问题清单（不修数据）。
/// ② 调用时机：恢复完成之后。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal sealed class RewindValidator : IRewindValidator
{
    public bool Validate(out string reason)
    {
        var problems = new List<string>();
        var state = CombatAccess.State;
        var player = CombatAccess.LocalPlayer;
        if (state is null || player?.PlayerCombatState is not { } combat)
        {
            reason = "no combat state";
            return false;
        }

        var registered = Reflect.Get<List<CardModel>>(state, Coordinates.CombatState.AllCards)?.ToHashSet();
        var seenInCombatPiles = new HashSet<CardModel>();

        foreach (var pile in combat.AllPiles)
        {
            foreach (var card in pile.Cards)
            {
                if (card.Owner != player)
                {
                    problems.Add($"{card.Id.Entry} owner mismatch in {pile.Type}");
                }

                if (!pile.IsCombatPile)
                {
                    continue;
                }

                if (registered is not null && !registered.Contains(card))
                {
                    problems.Add($"{card.Id.Entry} missing from CombatState._allCards");
                }

                if (!seenInCombatPiles.Add(card))
                {
                    problems.Add($"{card.Id.Entry} exists in multiple combat piles");
                }
            }
        }

        if (!RunManager.Instance.ActionQueueSet.IsEmpty)
        {
            problems.Add("action queue is not empty");
        }

        if (!Reflect.AllZeroValues(CombatManager.Instance, Coordinates.CombatManager.CardOrPotionEffectDepth))
        {
            problems.Add("card/potion effect depth is non-zero");
        }

        var creatureIds = state.Creatures.Where(c => c.CombatId.HasValue).Select(c => c.CombatId!.Value).ToArray();
        if (creatureIds.Distinct().Count() != creatureIds.Length)
        {
            problems.Add("creature combat ids are not unique");
        }

        reason = problems.Count == 0 ? string.Empty : string.Join("; ", problems);
        return problems.Count == 0;
    }
}
