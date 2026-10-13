using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.EnemyTime.Patches;

/// <summary>
/// C11 的"接管"支路：对"已经被我们托管的怪"跳过原生掷招。
/// </summary>
/// <remarks>
/// <para>
/// 不接管的话，每次玩家回合开始原生都会先 <c>RollMove</c>（推进 <c>_currentState</c>、写 <c>StateLog</c>、
/// 消耗 <c>RunRng.MonsterAi</c>），而 <c>StateLog</c> 正是随机分支状态的权重 / 冷却 / <c>UseOnlyOnce</c> 判据，
/// 会悄悄改掉我们排好的牌堆语义。
/// </para>
/// <para>
/// 跳过是安全的：<c>SetMoveImmediate</c> 内部本来就会 <c>ForceCurrentState</c>，链不会断；
/// <b>牌堆为空（= 没被托管）时不跳过</b>，让原生掷招继续跑。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.RollMove))]
internal static class IntentRollPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MonsterModel __instance)
    {
        try
        {
            if (!EnemyTimeScope.MasterEnabled || !EnemyTimeScope.ManagedRollMove)
            {
                return true;
            }

            // 角色门：不是永世沙漏的局里绝不接管掷招（重则改掉别的角色的怪物行为）。
            if (!CharacterGate.IsActive)
            {
                return true;
            }

            var creature = __instance.Creature;
            if (creature?.Side != CombatSide.Enemy || !IntentDeck.IsManaged(creature))
            {
                return true;
            }

            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} C11 接管 RollMove：跳过 {MonsterAccess.IdOf(creature)} 的原生掷招"
                + $"（next={__instance.NextMove.Id}，由解析器在玩家回合开始设定）");
            return false;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} C11 补丁异常（放行）：{ex.GetType().Name}: {ex.Message}");
            return true;
        }
    }
}
