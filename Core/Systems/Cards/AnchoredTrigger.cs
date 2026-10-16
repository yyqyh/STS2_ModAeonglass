using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Rare;
using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 「锚定之物」（NEW-8）的两件事：<b>起手进弃牌堆</b> + <b>进入手牌时对全体 90 伤</b>。
/// </summary>
/// <remarks>
/// <para>
/// ① 起手进弃牌堆：在<b>初始洗牌</b>那一拍（<c>CardPile.RandomizeOrderInternal</c> → <c>Hook.ModifyShuffleOrder(isInitialShuffle: true)</c>）
/// 把它从"要洗进抽牌堆的那批"里摘出来、改放战斗弃牌堆。挂点借起始遗物的 <c>ModifyShuffleOrder</c>
/// （本模组 迟滞 就是这么挂的），不新增补丁。
/// </para>
/// <para>
/// ② 进手牌触发：订阅<b>手牌堆</b>的 <c>CardAdded</c>（本体事件）⇒ 抽牌 / 检索 / 回手 / 唤回 全路径覆盖；
/// 战斗开始订阅、战斗结束退订（形状照 <c>RiotPileWatch</c>）。回调整体 try/catch，异常只降级。
/// </para>
/// <para>
/// ③ 触发次数：每次进手都结算（用户 2026-10-16 口径）。靠"这一张确实在手牌堆里"这道判定避免读档重挂卡面时的重复结算。
/// </para>
/// </remarks>
internal static class AnchoredTrigger
{
    /// <summary>进入手牌时对全体敌人造成多少伤害。</summary>
    private const decimal EnterHandDamage = 90m;

    private static readonly Dictionary<CardPile, Action<CardModel>> Watched = [];

    private static bool _registered;

    /// <summary>由 <c>Main.Initialize</c> 装配一次。</summary>
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
                if (player.PlayerCombatState is not { } state || Watched.ContainsKey(state.Hand))
                {
                    continue;
                }

                var hand = state.Hand;
                Action<CardModel> handler = card => OnCardAddedToHand(player, card);
                hand.CardAdded += handler;
                Watched[hand] = handler;
            }
        });

        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(_ =>
        {
            foreach (var (pile, handler) in Watched)
            {
                try
                {
                    pile.CardAdded -= handler;
                }
                catch (Exception ex)
                {
                    Const.Logger.Warn(
                        $"[{Const.ModId}] {Const.Prefix.Host} 锚定之物退订失败（忽略）：{ex.GetType().Name}");
                }
            }

            Watched.Clear();
        });
    }

    /// <summary>
    /// 初始洗牌那一刻：把「锚定之物」从抽牌堆摘出来、改放战斗弃牌堆（用户口径：起手进弃牌堆）。
    /// </summary>
    /// <remarks>
    /// ⚠️ 必须由 <c>AeonglassCore.ModifyShuffleOrder</c> 在<b>迟滞那一段之前</b>调：它自己也带迟滞，
    /// 若先被迟滞分支从<b>抽牌堆的那份列表</b>里摘掉（初始洗牌传的就是抽牌堆本体列表），这张牌会落到"哪个堆都不在"。
    /// </remarks>
    public static void PlaceStartInDiscard(Player player, List<CardModel> shuffleBatch)
    {
        try
        {
            if (PileType.Discard.GetPile(player) is not { } discard)
            {
                return;
            }

            var moved = 0;
            foreach (var card in shuffleBatch.Where(c => c is AeonglassAnchored).ToList())
            {
                shuffleBatch.Remove(card);
                if (!discard.Cards.Contains(card))
                {
                    discard.AddInternal(card);
                    moved++;
                }
            }

            if (moved > 0)
            {
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Host} 锚定之物：起手 → 弃牌堆（{moved} 张，抽牌堆 {shuffleBatch.Count} 张）");
            }
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Host} 锚定之物起手落点失败（留在抽牌堆）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void OnCardAddedToHand(Player player, CardModel card)
    {
        try
        {
            if (card is not AeonglassAnchored || card.Pile?.Type != PileType.Hand)
            {
                return;
            }

            _ = DamageAllEnemiesAsync(player, card);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Host} 锚定之物进手牌回调异常（忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task DamageAllEnemiesAsync(Player player, CardModel card)
    {
        try
        {
            if (CombatManager.Instance.IsOverOrEnding || player.Creature.CombatState is not { } combat)
            {
                return;
            }

            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Host} 锚定之物：进入手牌 ⇒ 对所有敌人 {EnterHandDamage} 伤");

            await DamageCmd.Attack(EnterHandDamage)
                .FromCard(card, null)
                .TargetingAllOpponents(combat)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(new BlockingPlayerChoiceContext());
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Host} 锚定之物结算异常（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
