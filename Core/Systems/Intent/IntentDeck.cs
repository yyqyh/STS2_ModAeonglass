using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>意图牌堆的工厂与惰性构建（一只怪一份 <c>EnemyIntentState</c> 的三堆循环）。</summary>
/// <remarks>
/// ① 职责：状态查找 / 建立（<c>Ensure</c>）、洗牌建堆、描述与守恒自检；抽弃洗流程在 <c>IntentCycle</c> / <c>IntentHandCycle</c>。
/// ② 挂点与时机：<c>IntentTurnFlow.PrepareTurn</c> 与 UI 读状态时调；该怪第一次要出牌时才建堆。
/// ③ 依赖：<c>IntentCard</c>/<c>IntentOps</c>、本体 <c>MonsterModel.States</c>（意图卡本尊 = 现成 <c>MoveState</c>）。
/// ④ ⚠️ 卡本尊用本体已有的 <c>MoveState</c>（零注册风险、天然 follow-up、原生 UI 能渲染），工厂只负责"放哪些进去 + 元数据"；
/// 惰性构建覆盖开局批次与召唤物，多怪各自建堆。
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
        var (pool, filled, capped) = BuildPool(IntentCycle.BuildCards(creature).ToList());
        // 构建完**先洗一次**：用户口径「抽牌完全随机」—— 抽牌堆的初始顺序也得是随机的，
        // 否则前几回合会按构建顺序（id 排序 + 循环复制）出牌。洗回时同样随机（见 IntentTurnFlow）。
        IntentCycle.Shuffle(pool, creature);
        state.Deck.AddRange(pool);

        state.IntentCount = state.Deck.Count;
        state.Total = state.IntentCount;
        States[creature] = state;
        _builds++;

        // 建堆也是一次"状态变化"（卡带第一次画出来要靠它）。
        IntentOps.Touch(state);

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
