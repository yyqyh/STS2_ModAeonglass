using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 「指向意图」的动作卡<b>共用执行口</b>：把玩家点中的意图交给动作。
/// </summary>
/// <remarks>
/// ① 职责：<c>ApplyPointed</c>。
/// ② 调用时机：动作卡（提前/威吓/思维剥夺）打出时。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class IntentActionCard
{
    /// <summary>
    /// **当前形态：原版卡牌指向** —— 玩家点的那张意图卡由侧表带回。
    /// </summary>
    /// <param name="pointed">
    /// 指向链路换回来的那只怪（= 意图卡所属的怪，来自 <c>cardPlay.Target</c>）。
    /// ⚠️ 多怪场里**必须**用它：以前退化成"第一只活怪"，点 B 怪的意图会落到 A 怪身上（`IntentMoves` 找不到那张牌 → 整卡白打）。
    /// </param>
    public static Task ApplyPointed(
        Player player,
        Creature? pointed,
        string verb,
        Func<Player, Creature, IntentCard, string> action)
    {
        try
        {
            var target = pointed is { IsAlive: true } ? pointed : MonsterAccess.LivingEnemies.FirstOrDefault();
            if (target is null)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」：场上没有活着的怪（什么都不做）");
                return Task.CompletedTask;
            }

            if (IntentTargetRegistry.TakePending() is not { } chosen)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」：这次没有指向到任何意图（已取消）");
                return Task.CompletedTask;
            }

            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」（原版指向）→ {action(player, target, chosen)}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」抛异常（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>直连兜底：不弹界面、不接点击，直接对第一只活着的怪执行非指向版动词。</summary>
    public static Task ApplyDirect(Player player, string verb, Func<Player, Creature, string> action)
    {
        try
        {
            if (MonsterAccess.LivingEnemies.FirstOrDefault() is not { } target)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」：场上没有活着的怪（什么都不做）");
                return Task.CompletedTask;
            }

            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」（直连）→ {action(player, target)}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」抛异常（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
