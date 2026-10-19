using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.FrozenForm;

/// <summary>
/// 「冻结形态」的<b>回放侧</b>：下一场战斗的第一个玩家回合开始时，把记录堆里的能力全部自动打出。
/// </summary>
/// <remarks>
/// <para>
/// ① 时机取 <c>SideTurnStartingEvent</c>（玩家侧）而不是 <c>CombatStartingEvent</c>：
/// 前者保证 <c>PlayerCombatState</c> 已就绪、战斗已真正开始；每场只回放一次（引用去重，
/// 战斗结束时清空）。语义仍是"下场战斗开始时"——玩家看到的就是开场第一回合自动打出。
/// </para>
/// <para>
/// ② 目标规则（用户 2026-10-18 口径）：优先自己 / 合法目标 ——
/// <c>Self</c> 取玩家自己，<c>AnyEnemy</c> 取当前第一只活怪，其余交给本体 AutoPlay 的默认规则。
/// </para>
/// <para>
/// ③ 回放后整堆取空（不跨场累积）；单张失败只记 Warn，继续打下一张，且不会把剩余牌留回堆里。
/// </para>
/// </remarks>
internal static class FrozenFormReplay
{
    private static ICombatState? _replayedCombat;

    /// <summary>由 <see cref="FrozenFormModule" /> 装配一次。</summary>
    public static void Register()
    {
        RitsuLibFramework.SubscribeLifecycle<SideTurnStartingEvent>(evt =>
        {
            if (evt.Side != CombatSide.Player || !CharacterGate.IsActive)
            {
                return;
            }

            if (ReferenceEquals(_replayedCombat, evt.CombatState))
            {
                return;
            }

            _replayedCombat = evt.CombatState;
            TaskHelper.RunSafely(ReplayAsync(evt.CombatState));
        });

        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(_ => _replayedCombat = null);
    }

    /// <summary>
    /// 立即回放（也供 headless 测试直接调用）：取空每个玩家的记录堆并逐张自动打出。
    /// </summary>
    internal static async Task ReplayAsync(ICombatState combat)
    {
        foreach (var player in combat.Players)
        {
            var cards = FrozenFormStore.TakeAll(player);
            if (cards.Count == 0)
            {
                continue;
            }

            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.FrozenForm} 下场开始 ⇒ 自动打出记录的 {cards.Count} 张能力"
                + $"（{string.Join(", ", cards.Select(card => card.Id.Entry))}）");

            foreach (var card in cards)
            {
                await PlayOneAsync(combat, player, card);
            }
        }
    }

    private static async Task PlayOneAsync(ICombatState combat, Player player, CardModel card)
    {
        FrozenFormRecorder.MarkReplaying(card);
        try
        {
            // 从 run 持久堆搬进战斗 Play 堆（AutoPlay 要求卡已在战斗作用域里）。
            await CardPileCmd.Add(card, PileType.Play);
            var target = TargetFor(card, player, combat);
            await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), card, target);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.FrozenForm} 回放 {card.Id.Entry} 失败（已跳过）："
                + $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            FrozenFormRecorder.UnmarkReplaying(card);
        }
    }

    /// <summary>优先自己 / 合法目标；其余返回 null（交给 AutoPlay 的默认规则）。</summary>
    private static Creature? TargetFor(CardModel card, Player player, ICombatState combat) =>
        card.TargetType switch
        {
            TargetType.Self => player.Creature,
            TargetType.AnyEnemy => combat.HittableEnemies.FirstOrDefault(),
            _ => null,
        };
}
