namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>一只怪的意图状态存储：三堆（抽 / 手 / 弃）+ 消耗历史 + 守恒基数。</summary>
/// <remarks>
/// ① 职责：只放状态字段与一个读数用的 <c>Counts()</c>；所有写操作走 <c>IntentOps</c> / <c>IntentCycle</c>。
/// ② 挂点与时机：由 <c>IntentDeck.Ensure</c> 惰性建出，按 <c>Creature</c> 存在 <c>IntentDeck</c> 的表里。
/// ③ 依赖：<c>IntentCard</c>（只存引用）。
/// ④ ⚠️ 字段即语义：<c>Deck[0]</c> = 下一个被抽到，<c>Hand[0]</c> = 解析的起点；改顺序请走 <c>IntentOps</c> 以维持守恒。
/// </remarks>
internal sealed class EnemyIntentState(string monsterId)
{
    public string MonsterId { get; } = monsterId;

    /// <summary>
    /// 状态版本号：任何一次写操作之后由 <c>IntentOps.Touch</c> 自增。
    /// </summary>
    /// <remarks>
    /// <b>只给表现层用</b>：卡带靠"整数比较"决定要不要重画（代替原来每 0.2 s 拼一遍字符串签名）。
    /// 它**不参与**守恒 / 判定 / 随机等任何机制口径，加的时候只管自增。
    /// </remarks>
    public int Revision { get; private set; }

    /// <summary>标记"这个状态变过了"（只自增，不做别的）。</summary>
    internal void BumpRevision() => Revision++;

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
