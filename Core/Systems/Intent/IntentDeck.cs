using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 一只怪的意图状态：<b>三堆</b>（抽牌堆 / 手牌 / 弃牌堆）+ 消耗历史 + 本次决策。
/// </summary>
/// <remarks>
/// 新模型（2026-10-11 用户定稿）：像玩家一样三堆、**没有消耗堆**（虚无被消耗时直接移除）。
/// 抽牌堆空时把**弃牌堆洗回**抽牌堆继续抽 ⇒ 模拟真实循环。
/// 守恒式：<c>deck + hand + discard + consumed + voidsSpent == total</c>。
/// </remarks>
internal sealed class EnemyIntentState(string monsterId)
{
    public string MonsterId { get; } = monsterId;

    /// <summary>抽牌堆：队首 = 下一个被抽到。</summary>
    public List<IntentCard> Deck { get; } = [];

    /// <summary>手牌：索引 0 = 第一张（解析时从它开始扫）。</summary>
    public List<IntentCard> Hand { get; } = [];

    /// <summary>弃牌堆：打出的 / 弃掉的 / <b>玩家塞进来的虚无</b>。</summary>
    public List<IntentCard> Discard { get; } = [];

    /// <summary>被"消耗"掉的意图卡 Id（已永久离开循环；只是历史，参与守恒）。</summary>
    public List<string> Consumed { get; } = [];

    /// <summary>已经被直接移除的虚无张数（<c>once</c> 口径下抽到即移除 ⇒ 参与守恒）。</summary>
    public int VoidsSpent { get; set; }

    /// <summary>本回合会被引擎执行的那张（= 我们上次 <c>SetMoveImmediate</c> 设进去的）。</summary>
    public IntentCard? Pending { get; set; }

    /// <summary>本回合的判定原因（面板的"判定行"）。</summary>
    public string Decision { get; set; } = "-";

    /// <summary>上一次敌人回合是否真的开始了（用来判断 <see cref="Pending" /> 有没有被打出）。</summary>
    public bool EnemyTurnSeen { get; set; }

    /// <summary>抽牌数 N = 该怪的<b>意图卡总数</b>（不含虚无）。</summary>
    public int IntentCount { get; set; }

    /// <summary>守恒基数 = 意图卡总数 + 曾经塞进来的虚无总数。</summary>
    public int Total { get; set; }

    /// <summary>洗回次数（诊断用：证明"弃牌堆洗回抽牌堆"真的发生了）。</summary>
    public int Reshuffles { get; set; }

    /// <summary>塞进来的虚无总数。</summary>
    public int VoidsInjected { get; set; }

    public string Counts() =>
        $"deck {Deck.Count} hand {Hand.Count} discard {Discard.Count} "
        + $"consumed {Consumed.Count} voidsSpent {VoidsSpent} total {Total} reshuffles {Reshuffles}";
}

/// <summary>
/// 意图牌堆的工厂与惰性构建。
/// </summary>
/// <remarks>
/// <para>
/// <b>关键取舍</b>：意图卡的本尊就是本体 <c>States</c> 里已有的 <c>MoveState</c>
/// （零注册风险、天然能 follow-up、天然能被原生 UI 渲染）；工厂只负责"把哪些 <c>MoveState</c>
/// 放进牌堆 + 给元数据"。
/// </para>
/// <para>
/// <b>惰性构建</b>：该怪第一次需要出牌时才现场建（覆盖开局批次与召唤物，多怪各自建堆）。
/// </para>
/// </remarks>
internal static class IntentDeck
{
    /// <summary>怪物意图"卡组"的固定张数 x（用户 2026-10-12：先设 10）。</summary>
    public const int DeckSizeX = 10;

    /// <summary>每回合抽牌数（固定 5，**完全随机**）。</summary>
    public const int DrawPerTurn = 5;

    private static readonly Dictionary<Creature, EnemyIntentState> States =
        new(ReferenceEqualityComparer.Instance);

    private static int _builds;

    public static int Builds => _builds;

    public static void Reset()
    {
        States.Clear();
        _builds = 0;
    }

    public static bool TryGet(Creature? creature, out EnemyIntentState state)
    {
        if (creature is not null && States.TryGetValue(creature, out var found))
        {
            state = found;
            return true;
        }

        state = null!;
        return false;
    }

    public static IEnumerable<EnemyIntentState> AllStates() => States.Values;

    public static IEnumerable<KeyValuePair<Creature, EnemyIntentState>> Entries() => States;

    /// <summary>
    /// C11 的接管判据：只有"已经建过牌堆、且牌堆非空"的怪才跳过原生掷招。
    /// </summary>
    /// <remarks>
    /// 牌堆为空时必须让原生掷招继续跑 —— 否则 <c>NextMove</c> 会停在 <c>UNSET_MOVE</c> 上，
    /// <c>TakeTurn</c> 会直接抛。
    /// </remarks>
    public static bool IsManaged(Creature? creature) =>
        IntentRules.MasterEnabled && IntentRules.ManagedRollMove && creature is not null &&
        States.TryGetValue(creature, out var state) && state.IntentCount > 0;

    /// <summary>惰性构建：第一次需要这只怪的意图时现场建，不依赖任何入口是否被覆盖。</summary>
    public static EnemyIntentState Ensure(Creature creature)
    {
        if (States.TryGetValue(creature, out var existing))
        {
            return existing;
        }

        var state = new EnemyIntentState(MonsterAccess.IdOf(creature));
        var (pool, filled, capped) = BuildPool(BuildCards(creature).ToList());
        // 构建完**先洗一次**：用户口径「抽牌完全随机」—— 抽牌堆的初始顺序也得是随机的，
        // 否则前几回合会按构建顺序（id 排序 + 循环复制）出牌。洗回时同样随机（见 IntentTurnFlow）。
        Shuffle(pool, creature);
        state.Deck.AddRange(pool);

        state.IntentCount = state.Deck.Count;
        state.Total = state.IntentCount;
        States[creature] = state;
        _builds++;

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Intent} 意图牌堆构建 #{_builds}（惰性）monster={state.MonsterId} "
            + $"卡组 {state.IntentCount}/{DeckSizeX} 张 [{string.Join(",", state.Deck.Select(c => c.MoveId))}]"
            + $"（复制补足 +{filled}，超上限截断 -{capped}，每回合随机抽 {DrawPerTurn} 张）");
        return state;
    }

    /// <summary>
    /// 把"这只怪实际可用的意图"整理成固定 <see cref="DeckSizeX" /> 张的卡组。
    /// </summary>
    /// <remarks>
    /// 用户 2026-10-12 口径：**可用意图数 &gt; x ⇒ 只取 x 张**（按原顺序截断）；
    /// **&lt; x ⇒ 按原顺序循环复制补足到 x**。补足用的是 <see cref="IntentCard.Clone" />（各自独立一份元数据），
    /// 所以重复的意图可以同时出现在手上、"消耗"也只影响被消耗的那一份。
    /// </remarks>
    private static (List<IntentCard> Pool, int Filled, int Capped) BuildPool(List<IntentCard> baseCards)
    {
        if (baseCards.Count == 0)
        {
            return ([], 0, 0);
        }

        if (baseCards.Count >= DeckSizeX)
        {
            return (baseCards.Take(DeckSizeX).ToList(), 0, baseCards.Count - DeckSizeX);
        }

        var pool = new List<IntentCard>(baseCards);
        for (var i = 0; pool.Count < DeckSizeX; i++)
        {
            pool.Add(baseCards[i % baseCards.Count].Clone());
        }

        return (pool, pool.Count - baseCards.Count, 0);
    }

    /// <summary>洗牌（用怪物自己的 AI 随机流 ⇒ 跑局内可复现、回档也被快照覆盖；拿不到就用 <c>Random.Shared</c>）。</summary>
    private static void Shuffle(List<IntentCard> cards, Creature creature)
    {
        var rng = creature.Monster?.RunRng.MonsterAi;
        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = rng is null ? Random.Shared.Next(i + 1) : rng.NextInt(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    /// <summary>工厂：把怪物的 <c>MoveState</c> 集合生产成一叠意图卡（每场战斗一次）。</summary>
    private static IEnumerable<IntentCard> BuildCards(Creature creature)
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

    private static bool IsSpecial(string moveId) =>
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

    /// <summary>一行文本（面板 / 日志用）。</summary>
    public static string Describe(Creature? creature)
    {
        if (creature is null)
        {
            return "没有目标怪（先进战斗）";
        }

        if (!TryGet(creature, out var state))
        {
            return $"{MonsterAccess.IdOf(creature)}：意图牌堆未构建（等它的第一个玩家回合开始会自动建）";
        }

        var hand = state.Hand.Count == 0 ? "(空)" : string.Join("  ", state.Hand.Select((c, i) => $"{i}:{c.Describe()}"));
        var deck = state.Deck.Count == 0 ? "(空)" : string.Join(",", state.Deck.Select(c => c.MoveId));
        var discard = state.Discard.Count == 0 ? "(空)" : string.Join(",", state.Discard.Select(c => c.MoveId));
        return $"{state.MonsterId}｜{state.Counts()}｜hand[ {hand} ] deck[{deck}] discard[{discard}]";
    }

    /// <summary>C8 的守恒校验（新三堆版）。</summary>
    public static string ConservationCheck(Creature creature)
    {
        var state = Ensure(creature);
        // 三堆循环版（用户 2026-10-12 澄清：「运行阶段模拟玩家的抽弃手牌循环」）：
        // 抽牌堆 + 手牌 + 弃牌堆 + 消耗 + 已结算虚无 == 总量（构建到 x + 之后塞进来的虚无）。
        var sum = state.Deck.Count + state.Hand.Count + state.Discard.Count
                  + state.Consumed.Count + state.VoidsSpent;
        var ok = sum == state.Total;
        var text = $"守恒 deck({state.Deck.Count}) + hand({state.Hand.Count}) + discard({state.Discard.Count}) "
                   + $"+ consumed({state.Consumed.Count}) + voidsSpent({state.VoidsSpent}) = {sum} "
                   + $"vs total({state.Total}) → {(ok ? "OK" : "BROKEN")}";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} {text}");
        return text;
    }
}
