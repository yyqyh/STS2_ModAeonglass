using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Intent.Patches;

/// <summary>操作时间的"接管"支路：对<b>已被我们托管的怪</b>跳过原生掷招。</summary>
/// <remarks>
/// ① 职责：拦 <c>MonsterModel.RollMove</c>，命中托管怪时返回 <c>false</c>（真身由 <c>IntentTurnFlow</c> 写 <c>NextMove</c>）。
/// ② 挂点与时机：Harmony 前缀补丁，怪物每次掷招时跑；<b>牌堆为空（= 没被托管）时放行</b>，让原生掷招继续。
/// ③ 依赖：<c>IntentRules</c>（托管开关）、<c>IntentDeck</c>（是否托管）、角色门（不是永世沙漏的局绝不接管）。
/// ④ ⚠️ 不接管的话原生会推进 <c>_currentState</c>、写 <c>StateLog</c>、耗 <c>RunRng.MonsterAi</c>，
/// 而 <c>StateLog</c> 正是分支权重 / 冷却 / <c>UseOnlyOnce</c> 的判据，会悄悄改掉我们排好的牌堆语义；
/// 跳过是安全的：<c>SetMoveImmediate</c> 内部本来就会 <c>ForceCurrentState</c>，链不会断。
/// </remarks>
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.RollMove))]

internal static class IntentRollPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MonsterModel __instance)
    {
        try
        {
            if (!IntentRules.MasterEnabled || !IntentRules.ManagedRollMove)
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
