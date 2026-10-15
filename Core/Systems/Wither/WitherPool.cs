using MegaCrit.Sts2.Core.Commands;
using STS2_Aeonglass.Core.Content.Cards.Token;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.ValueProps;

using STS2_Aeonglass.Core.Systems.AlphaBeta;

using STS2_Aeonglass.Core.Systems.Intent;
namespace STS2_Aeonglass.Core.Systems.Wither;

/// <summary>
/// 凋萎 = 往怪物的<b>弃牌堆</b>里塞"虚无"。
/// </summary>
/// <remarks>
/// <para>
/// <b>新三堆口径（2026-10-11 定稿）</b>：虚无进的是**弃牌堆**，不是手牌 ⇒ 它要等
/// "抽牌堆空 → 弃牌堆洗回 → 被抽到"才生效（所以塞进去可能延迟很久才吃到）。
/// </para>
/// <para>
/// 抽出后的去向由 <see cref="VoidDrawMode" /> 决定（默认 = 惩戒一次后**直接移除**，
/// 保持早先拍定的"只生效一次"）；惩戒 = 扣血（<c>ValueProp.Unpowered</c>）+ 可选"吃掉一张意图"
/// （怪物没有能量系统，所以原版 Void 的"扣 1 能量"换成扣血）。
/// </para>
/// </remarks>
internal static class WitherPool
{
    // ==================== 给"净化"（Step 8 卡表）留的干净接口 ====================

    /// <summary>某只怪现在**手牌里**有几张凋萎（还能被"净化/剥夺"处理的那部分）。</summary>
    public static int WithersInHand(Creature creature) =>
        IntentDeck.TryGet(creature, out var state) ? state.Hand.Count(c => c.IsVoid) : 0;

    /// <summary>某只怪**弃牌堆里**有几张凋萎（还没被抽到的污染）。</summary>
    public static int WithersInDiscard(Creature creature) =>
        IntentDeck.TryGet(creature, out var state) ? state.Discard.Count(c => c.IsVoid) : 0;

    /// <summary>某只怪**抽牌堆里**有几张凋萎（「枯萎」升级档要数它）。</summary>
    public static int WithersInDeck(Creature creature) =>
        IntentDeck.TryGet(creature, out var state) ? state.Deck.Count(c => c.IsVoid) : 0;

    /// <summary>
    /// 这只怪**场上（三个意图堆）**一共有几张凋萎 —— 「净化」（WTH-3）按它算伤害。
    /// </summary>
    /// <remarks>三堆都算：抽牌堆里还没抽到的、手上的、弃牌堆里的。</remarks>
    public static int WithersTotal(Creature creature) =>
        IntentDeck.TryGet(creature, out var state)
            ? state.Hand.Count(c => c.IsVoid) + state.Discard.Count(c => c.IsVoid) + state.Deck.Count(c => c.IsVoid)
            : 0;

    /// <summary>
    /// 清掉这只怪**三个意图堆里的所有凋萎**（「净化」用），返回清掉几张。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PurgeFromDiscard" /> 的区别：那个只动弃牌堆（挑一张），这个把**整只怪身上的污染**一次清空。
    /// 只记一笔 <c>VoidsSpent</c>（与另外两个清法同口径，别双计）。
    /// </remarks>
    public static int PurgeAll(Creature creature)
    {
        if (!IntentDeck.TryGet(creature, out var state))
        {
            return 0;
        }

        var purged = 0;
        foreach (var pile in new[] { state.Hand, state.Discard, state.Deck })
        {
            foreach (var card in pile.Where(c => c.IsVoid).ToList())
            {
                pile.Remove(card);
                purged++;
            }
        }

        state.VoidsSpent += purged;
        if (purged > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Wither} 净化：清空 {MonsterAccess.IdOf(creature)} 的全部凋萎 {purged} 张"
                + $"｜{state.Counts()}");
        }

        return purged;
    }

    /// <summary>
    /// 从该怪**手牌**里拿走最多 <paramref name="count" /> 张凋萎（进"消耗"，不再参与循环）。
    /// </summary>
    /// <remarks>这是"净化"要用的写入口；"剥夺"走的是另一条（进弃牌堆、会被洗回）—— 两者语义不同，都留着。</remarks>
    public static int PurgeFromHand(Creature creature, int count)
    {
        if (count <= 0 || !IntentDeck.TryGet(creature, out var state))
        {
            return 0;
        }

        var taken = 0;
        foreach (var card in state.Hand.Where(c => c.IsVoid).ToList())
        {
            if (taken >= count)
            {
                break;
            }

            state.Hand.Remove(card);
            // ⚠️ 只记一笔：`VoidsSpent` 的语义就是"结算/移除掉的凋萎"；
            // 再往 `Consumed` 记一次会让守恒式（deck+hand+discard+consumed+voidsSpent==total）**双计**。
            state.VoidsSpent++;
            taken++;
        }

        if (taken > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Wither} 净化：从手牌拿走 {taken} 张凋萎（剩 hand {WithersInHand(creature)}"
                + $" / discard {WithersInDiscard(creature)}）");
        }

        return taken;
    }

    /// <summary>玩家往弃牌堆塞虚无（可塞多张）。</summary>
    public static string Inject(Creature creature, int count)
    {
        var state = IntentDeck.Ensure(creature);
        if (count <= 0)
        {
            return "skip: count<=0";
        }

        // α or β（永时副）：「给予凋萎」额外 +1 张（仅当本局还没用过回溯）。
        var passiveOwner = MonsterAccess.Combat?.PlayerCreatures?.FirstOrDefault()?.Player;
        if (passiveOwner is not null && AlphaBetaPassive.WitherBonus(passiveOwner))
        {
            count += 1;
            Const.Logger.Info($"[{Const.ModId}] α or β（永时副）：给予凋萎额外 +1 张");
        }

        // 凋萎要做成**真卡实例**（用户 2026-10-12）：拿不到玩家（不在战斗里）就退回纯元数据，
        // 但那样就没有"可多次强化"的数值 —— 日志会说明。
        var player = MonsterAccess.Combat?.PlayerCreatures?.FirstOrDefault()?.Player;

        for (var i = 0; i < count; i++)
        {
            // 放进弃牌堆：它要等"洗回抽牌堆"才可能被抽到（延迟生效，符合"抽/弃/洗回"的循环）。
            var card = IntentCard.Void();
            if (player is not null)
            {
                try
                {
                    card.WitherCard = player.RunState.CreateCard(
                        MegaCrit.Sts2.Core.Models.ModelDb.Card<AeonglassWitherCard>(), player);
                }
                catch (Exception ex)
                {
                    Const.Logger.Warn(
                        $"[{Const.ModId}] {Const.Prefix.Wither} 凋萎卡实例创建失败（这一张退回元数据）："
                        + $"{ex.GetType().Name}: {ex.Message}");
                }
            }

            state.Discard.Add(card);
        }

        state.Total += count;
        state.VoidsInjected += count;

        var text = $"塞虚无 ×{count} → 弃牌堆｜{state.Counts()}｜discard[{DescribePile(state.Discard)}]";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} {text}");
        return text;
    }

    /// <summary>
    /// 从<b>怪物弃牌堆</b>清掉最多 <paramref name="count" /> 张凋萎（WTH-3 净化用），返回实际清掉几张。
    /// </summary>
    /// <remarks>只动弃牌堆：手牌里的凋萎照旧要走"行动后结算"那条路（<see cref="PurgeFromHand" /> 是另一条）。</remarks>
    public static int PurgeFromDiscard(Creature creature, int count)
    {
        if (count <= 0 || !IntentDeck.TryGet(creature, out var state))
        {
            return 0;
        }

        var purged = 0;
        foreach (var card in state.Discard.Where(c => c.IsVoid).Take(count).ToList())
        {
            state.Discard.Remove(card);
            state.VoidsSpent++;
            purged++;
        }

        if (purged > 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Wither} 净化：从弃牌堆清掉 {purged} 张凋萎｜{state.Counts()}");
        }

        return purged;
    }

    /// <summary>
    /// 虚无被<b>抽到</b>时的惩戒（异步游戏动作，走 TaskHelper 丢给主线程续体，不阻塞回合开始管线）。
    /// </summary>
    public static void ApplyPenalty(Creature creature, EnemyIntentState state, IntentCard voidCard, string cause) =>
        TaskHelper.RunSafely(ApplyPenaltyAsync(creature, state, voidCard, cause));

    public static async Task ApplyPenaltyAsync(
        Creature creature,
        EnemyIntentState state,
        IntentCard voidCard,
        string cause)
    {
        var parts = new List<string> { $"虚无 {cause}（第 {voidCard.TimesDrawn} 次被抽到）" };

        // 掉血量**读卡上的值**（可多次强化 ⇒ 3/4/5/6）；没有卡实例（降级路径）就用设置里的基础值。
        var damage = voidCard.HitPointsOf();
        if (damage > 0)
        {
            parts.Add($"扣血 {damage}(Unpowered)");
            await DealAsync(creature, damage);
        }

        if (IntentRules.VoidEatsIntent)
        {
            var eaten = state.Hand.FirstOrDefault(c => !c.IsVoid && !c.Eaten);
            if (eaten is not null)
            {
                eaten.Eaten = true;
                parts.Add($"吃掉意图 {eaten.MoveId}");
            }
        }

        if (damage <= 0 && !IntentRules.VoidEatsIntent)
        {
            parts.Add("无惩戒（设置里都关了）");
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} {string.Join(" / ", parts)}");
    }

    private static async Task DealAsync(Creature creature, int amount)
    {
        try
        {
            await CreatureCmd.Damage(
                new BlockingPlayerChoiceContext(),
                creature,
                amount,
                ValueProp.Unpowered,
                null,
                null);

            // Damage 自己不结算胜负：万一这一下把怪打死了，要主动补一次胜利判定，
            // 否则战斗会停在那里等下一次玩家动作。
            await CombatManager.Instance.CheckWinCondition();

            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} 惩戒生效：{MonsterAccess.IdOf(creature)} hp={creature.CurrentHp}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] {Const.Prefix.Wither} 惩戒失败：{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string DescribePile(IReadOnlyList<IntentCard> pile) =>
        pile.Count == 0 ? "(空)" : string.Join(",", pile.Select(c => c.MoveId));
}
