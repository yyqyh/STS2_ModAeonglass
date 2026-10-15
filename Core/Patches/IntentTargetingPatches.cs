using Godot;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Content.Cards.Basic;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// **原版卡牌指向**的三个落点（用户 2026-10-12：用 <c>TargetedNoCreature</c> 那种形态、
/// 只能指向我们的意图卡、不用选择器也不用我们自己接点击）。
/// </summary>
/// <remarks>
/// 链路（已核）：
/// <c>NMouseCardPlay</c> 起瞄准 → <c>NTargetManager.AllowedToTargetNode</c> 判合法节点 →
/// 悬停/确认 → 出牌 → <c>PlayCardAction</c> 里 <c>_card.IsValidTarget(target)</c> 再校验一次。
/// 所以：
/// <list type="number">
///   <item><description><b>IsValidTarget</b>：我们的卡 + 目标为 null（<c>TargetedNoCreature</c>）判为合法 —— 对应药水那条口径。</description></item>
///   <item><description><b>AllowedToTargetNode</b>：放行"我们登记过的意图卡节点"。</description></item>
///   <item><description><b>OnNodeHovered / FinishTargeting</b>：记下"最近指向的那张 / 确认那一张"，交给侧表。</description></item>
/// </list>
/// <b>全部只在"正在打出我们这三张动作卡"时生效</b>（<see cref="IsOurActionCard" /> / 节点是否登记过），
/// 其余卡与本体行为一点不改 ✔。
/// </remarks>
internal static class IntentTargetingPatches
{
    /// <summary>
    /// 是不是"我们的意图动作卡"（只有它们允许指向意图卡）。
    /// </summary>
    /// <remarks>
    /// 2026-10-13：三张测试动作卡已删 ⇒ 改成标记接口 <see cref="IIntentTargetCard" />，
    /// 卡表落地时让真正的动作卡实现它即可（指向机器本身完全不变）。
    /// </remarks>
    private static bool IsOurActionCard(CardModel? card) => card is IIntentTargetCard;

    /// <summary>① 目标为 null（非生物目标）时，我们这三张卡要判为合法。</summary>
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
    internal static class IsValidTargetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CardModel __instance, Creature? target, ref bool __result)
        {
            try
            {
                if (__result || target is not null || !IsOurActionCard(__instance))
                {
                    return;
                }

                // 只有"非生物目标"这一种情形才由我们兜底放行，其余照本体结果。
                if (__instance.TargetType == TargetType.TargetedNoCreature)
                {
                    __result = true;
                }
            }
            catch (Exception ex)
            {
                // 兜底：异常时保持本体判定（不改 __result）。
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} IsValidTarget 兜底失败（保持本体判定）：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>② 瞄准时放行"我们登记过的意图卡节点"。</summary>
    [HarmonyPatch(typeof(NTargetManager), nameof(NTargetManager.AllowedToTargetNode))]
    internal static class AllowedToTargetNodePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Node node, ref bool __result)
        {
            try
            {
                // ① 禁止指向敌人：这次瞄准是我们那三张卡起的 ⇒ 生物节点一律不放行（只能点意图卡）。
                if (__result && IntentTargetRegistry.AimingOurCard && node is NCreature)
                {
                    __result = false;
                    return;
                }

                if (!__result && IntentTargetRegistry.IsIntentNode(node))
                {
                    __result = true;
                    Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 瞄准：放行意图卡节点 ✔");
                }
            }
            catch (Exception ex)
            {
                // 兜底：异常时保持本体判定（不改 __result）。
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 目标节点放行判定失败（保持本体判定）：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>③a 悬停时记下"最近指向的意图卡"。</summary>
    [HarmonyPatch(typeof(NTargetManager), nameof(NTargetManager.OnNodeHovered))]
    internal static class NodeHoveredPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Node node)
        {
            try
            {
                IntentTargetRegistry.NoteHovered(node);
                if (IntentTargetRegistry.IsIntentNode(node))
                {
                    Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 瞄准：悬停到意图卡节点 ✔");
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 悬停记录失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>③b 玩家确认这次指向时，把"最近指向的那张"变成待取的值。</summary>
    [HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
    internal static class FinishTargetingPatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool cancel)
        {
            // 日志只在"这次瞄准至少和意图卡有关"时写 —— 别的角色/别的卡瞄准完不成我们的日志。
            var ours = IntentTargetRegistry.AimingOurCard || IntentTargetRegistry.HasPending;

            try
            {
                if (ours)
                {
                    Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 瞄准结束（cancel={cancel}）");
                }

                if (!cancel)
                {
                    IntentTargetRegistry.CommitHovered();
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 瞄准结束处理失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                // 无论成败都要复位：留着 true 会让"下一次瞄准"被误判成我们的。
                IntentTargetRegistry.AimingOurCard = false;
            }
        }
    }

    /// <summary>
    /// ⑤ 记住"这次瞄准是我们那三张卡起的" —— 只有这种瞄准才禁止指向生物。
    /// </summary>
    /// <remarks>
    /// 用 <c>Traverse</c> 取 <c>Card</c> 属性（不依赖它的可见性，本体改可见性也不会编译不过）。
    /// </remarks>
    [HarmonyPatch(typeof(NMouseCardPlay), "SingleCreatureTargeting")]
    internal static class SingleCreatureTargetingPatch
    {
        [HarmonyPrefix]
        private static void Prefix(NMouseCardPlay __instance)
        {
            try
            {
                var card = Traverse.Create(__instance).Property("Card").GetValue<CardModel?>();
                IntentTargetRegistry.AimingOurCard = IsOurActionCard(card);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 读取瞄准卡失败（按「不是我们的卡」处理）：{ex.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// ④ 把"被点中的意图卡节点"换成它所属怪的 <c>NCreature</c>（并把这张意图卡记进侧表）。
    /// </summary>
    /// <remarks>
    /// 本体 <c>SingleCreatureTargeting</c> 收到非生物/非玩家节点会抛异常；换成怪节点后它照常走通，
    /// 而卡牌结算用的是侧表里的意图卡（不读 <c>cardPlay.Target</c>）✔。
    /// 用 postfix 包一层返回的 Task（不改本体那段逻辑）；包装或替换失败时都<b>保持本体原节点</b>。
    /// </remarks>
    [HarmonyPatch(typeof(NTargetManager), nameof(NTargetManager.SelectionFinished))]
    internal static class SelectionFinishedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref Task<Node?> __result)
        {
            // 范围门：这一场没有登记任何意图卡节点 ⇒ 连包装都不做（本体拿到的还是原 Task）。
            if (!IntentTargetRegistry.HasNodes)
            {
                return;
            }

            var original = __result;

            try
            {
                __result = SubstituteAsync(original);
            }
            catch (Exception ex)
            {
                // 兜底：保持本体原 Task。
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 目标替换包装失败（保持本体原结果）：{ex.GetType().Name}: {ex.Message}");
            }
        }

        private static async Task<Node?> SubstituteAsync(Task<Node?> original)
        {
            var node = await original;

            try
            {
                var swapped = IntentTargetRegistry.SubstituteNode(node);
                if (!ReferenceEquals(swapped, node))
                {
                    Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 瞄准：点中意图卡 → 目标换成该怪节点 ✔");
                }

                return swapped;
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 目标替换失败（保持本体原节点）：{ex.GetType().Name}: {ex.Message}");
                return node;
            }
        }
    }
}

