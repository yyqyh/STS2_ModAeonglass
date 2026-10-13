using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// 三张"意图动作卡"共用的三个执行口（用户 2026-10-12：**当前用"原版卡牌指向"**，其余方案保留留拓展）。
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description><see cref="ApplyPointed" /> —— **当前形态**：原版卡牌指向（瞄准 → 点意图卡 → 结算），
///   三处放行/记录见 <c>Core/Patches/IntentTargetingPatches.cs</c>；</description></item>
///   <item><description><see cref="ApplyDirect" /> —— 直连兜底：不弹界面不接点击，直接对第一只怪执行；</description></item>
///   <item><description><see cref="ApplyAsync" /> —— 备选：本体选牌链路（挂自动选择器即不弹界面）。</description></item>
/// </list>
/// <para>
/// <b>保留策略（上级 2026-10-12 口径）</b>：三条都保留、都算正式方案，不属死代码。
/// <c>ApplyDirect</c> 的启用时机 = 指向链路出问题时顶上（一行切换，保证测试卡永远可用）；
/// <c>ApplyAsync</c> 的启用时机 = 将来要"从一列卡里挑"而不是"指向某张"（例如一次选多张意图）。
/// </para>
/// </remarks>
internal static class IntentActionCard
{
    /// <summary>备选方案开关：是否用<b>自动选择器</b>（不弹界面、直接取第一张）。</summary>
    private static readonly bool UseAutoSelector = true;

    /// <summary>**当前形态：原版卡牌指向** —— 玩家点的那张意图卡由侧表带回。</summary>
    public static Task ApplyPointed(Player player, string verb, Func<Player, Creature, IntentCard, string> action)
    {
        try
        {
            if (MonsterAccess.LivingEnemies.FirstOrDefault() is not { } target)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」：场上没有活着的怪（什么都不做）");
                return Task.CompletedTask;
            }

            if (IntentTargetRegistry.TakePending() is not { } chosen)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」：这次没有指向到任何意图（已取消）");
                return Task.CompletedTask;
            }

            var text = action(player, target, chosen);
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」（原版指向）→ {text}");
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

            var text = action(player, target);
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」（直连）→ {text}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」抛异常（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>备选：本体选牌链路（挂自动选择器 ⇒ 不弹界面；去掉则弹本体选牌界面）。</summary>
    public static async Task ApplyAsync(
        Player player,
        PlayerChoiceContext choiceContext,
        string verb,
        Func<Player, Creature, IntentCard, string> action)
    {
        try
        {
            if (MonsterAccess.LivingEnemies.FirstOrDefault() is not { } target)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」：场上没有活着的怪（什么都不做）");
                return;
            }

            var state = IntentDeck.Ensure(target);
            var models = new List<CardModel>();
            foreach (var card in state.Hand)
            {
                if (IntentCardBinder.InstanceFor(player, target, card) is { } model)
                {
                    models.Add(model);
                }
            }

            if (models.Count == 0)
            {
                Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」：没有造出任何意图卡（选牌跳过）");
                return;
            }

            var prompt = verb switch
            {
                "剥夺" => CardSelectorPrefs.DiscardSelectionPrompt,
                _ => CardSelectorPrefs.RemoveSelectionPrompt,
            };

            var prefs = new CardSelectorPrefs(prompt, 1)
            {
                Cancelable = true,
                PretendCardsCanBePlayed = true,
            };

            CardModel? picked;
            if (UseAutoSelector)
            {
                using (CardSelectCmd.PushSelector(new AeonglassIntentSelector()))
                {
                    picked = (await CardSelectCmd.FromSimpleGrid(choiceContext, models, player, prefs)).FirstOrDefault();
                }
            }
            else
            {
                picked = (await CardSelectCmd.FromSimpleGrid(choiceContext, models, player, prefs)).FirstOrDefault();
            }

            if (picked is null || IntentCardBinder.IntentOf(picked) is not { } chosen)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」：这次没选到意图（已取消或映射丢失）");
                return;
            }

            var text = action(player, target, chosen);
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」（选牌链路）→ {text}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡牌「{verb}」抛异常（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
