using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Uncommon;
using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「触顶」的卡外触发：当它位于抽牌堆顶时，立刻进入手牌。
/// </summary>
/// <remarks>
/// ① 订阅抽牌堆的 <c>CardAdded</c> / <c>CardRemoved</c>（本体事件）：抽走上面一张、或它被放到顶上都覆盖。
/// ② 回调在牌堆方法体内同步触发 ⇒ 不能直接 await：延迟一小拍再检查一次堆顶，并加 <c>Pulling</c> 防重入。
/// ③ 战斗开始订阅、战斗结束退订（形状照 <c>AnchoredTrigger</c>）；异常只降级。
/// </remarks>
internal static class DrawTopWatcher
{
    private static readonly Dictionary<CardPile, (Action<CardModel> Added, Action<CardModel> Removed)> Watched = [];

    private static readonly HashSet<PlayerCombatState> Pulling = [];

    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(evt =>
        {
            if (!CharacterGate.IsActive)
            {
                return;
            }

            foreach (var player in evt.CombatState?.Players ?? [])
            {
                if (player.PlayerCombatState is { } state)
                {
                    Watch(player, state);
                }
            }
        });

        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(_ =>
        {
            foreach (var (pile, handlers) in Watched)
            {
                try
                {
                    pile.CardAdded -= handlers.Added;
                    pile.CardRemoved -= handlers.Removed;
                }
                catch (Exception ex)
                {
                    Const.Logger.Warn(
                        $"[{Const.ModId}] {Const.Prefix.Host} 触顶退订失败（忽略）：{ex.GetType().Name}");
                }
            }

            Watched.Clear();
            Pulling.Clear();
        });
    }

    private static void Watch(Player player, PlayerCombatState state)
    {
        var draw = state.DrawPile;
        if (Watched.ContainsKey(draw))
        {
            return;
        }

        void Added(CardModel _) => Schedule(player, state);
        void Removed(CardModel _) => Schedule(player, state);

        draw.CardAdded += Added;
        draw.CardRemoved += Removed;
        Watched[draw] = (Added, Removed);

        Schedule(player, state);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 触顶：已盯住 {player.NetId} 的抽牌堆顶");
    }

    private static void Schedule(Player player, PlayerCombatState state)
    {
        if (Pulling.Contains(state))
        {
            return;
        }

        TaskHelper.RunSafely(PullSoonAsync(player, state));
    }

    private static async Task PullSoonAsync(Player player, PlayerCombatState state)
    {
        try
        {
            await Cmd.Wait(0.05f, ignoreCombatEnd: true);

            if (Pulling.Contains(state)
                || state.DrawPile.Cards.FirstOrDefault() is not AeonglassTouchTop card)
            {
                return;
            }

            Pulling.Add(state);
            try
            {
                await CardPileCmd.Add(card, PileType.Hand);
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Host} 触顶：{card.Id.Entry} 在抽牌堆顶 ⇒ 进入手牌");
            }
            finally
            {
                Pulling.Remove(state);
            }
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Host} 触顶回手失败（忽略）："
                + $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
