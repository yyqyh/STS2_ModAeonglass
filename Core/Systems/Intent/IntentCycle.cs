using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图的<b>构筑与解析</b>：建牌、洗牌、把意图卡解析回本体的 <c>MoveState</c>。
/// </summary>
/// <remarks>
/// ① 职责：<c>BuildCards</c>（按怪物的招式表建卡）/ <c>Shuffle</c> / <c>Resolve</c>（卡 → 本体招式）。
/// ② 调用时机：战斗开始建牌；每次解析器要"打出"某张意图时解析。
/// ③ 依赖：<c>IntentDeck</c>（状态）+ <c>MonsterAccess</c>（读本体招式状态机）。
/// ④ ⚠️ 特殊招式（招牌/眩晕等）不参与循环，判定只有 <c>IsSpecial</c> 一处。
/// </remarks>
internal static class IntentCycle
{

    /// <summary>洗牌（用怪物自己的 AI 随机流 ⇒ 跑局内可复现、回档也被快照覆盖；拿不到就用 <c>Random.Shared</c>）。</summary>
    internal static void Shuffle(List<IntentCard> cards, Creature creature)
    {
        var rng = creature.Monster?.RunRng.MonsterAi;
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = rng is null ? Random.Shared.Next(i + 1) : rng.NextInt(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    /// <summary>工厂：把怪物的 <c>MoveState</c> 集合生产成一叠意图卡（每场战斗一次）。</summary>
    internal static IEnumerable<IntentCard> BuildCards(Creature creature)
    {
        var machine = MonsterAccess.MachineOf(creature);
        if (machine is null)
        {
            yield break;
        }

        var excluded = 0;
        foreach (var move in machine.States.Values.OfType<MoveState>()
                     .Where(m => m.Id != Const.Intent.UnsetMoveId && m.Id != Const.Intent.StunnedStateId)
                     .OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            if (IntentRules.ExcludeSpecialStates && IsSpecial(move.Id))
            {
                excluded++;
                continue;
            }

            yield return new IntentCard(move.Id, isVoid: false) { State = move };
        }

        if (excluded > 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 工厂排除了 {excluded} 个特殊状态（DEAD/ENRAGED/STUN 等）");
        }
    }

    internal static bool IsSpecial(string moveId) =>
        Const.Intent.ExcludedStateFragments.Any(fragment =>
            moveId.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>把一张意图卡解析回 <c>MoveState</c>（优先用构建时抓到的引用）。</summary>
    public static MoveState? Resolve(Creature creature, IntentCard card)
    {
        if (card.IsVoid)
        {
            return null;
        }

        if (card.State is not null)
        {
            return card.State;
        }

        var machine = MonsterAccess.MachineOf(creature);
        if (machine is not null && machine.States.TryGetValue(card.MoveId, out var raw) && raw is MoveState move)
        {
            return move;
        }

        Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} 找不到意图状态 {card.MoveId}");
        return null;
    }
}
