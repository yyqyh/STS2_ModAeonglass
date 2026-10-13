using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>「迟滞」：带这个关键词的牌<b>不参与洗牌</b>（洗牌时跳过、留在弃牌堆）。</summary>
/// <remarks>
/// <para><b>规则层 v1.1 的四条口径</b>：</para>
/// <list type="number">
/// <item><description>带迟滞的牌**不参与洗牌**（跳过、留在弃牌堆）—— 由 <c>AeonglassCore.ModifyShuffleOrder</c> 执行。</description></item>
/// <item><description><b>战斗内限定</b>：洗牌只发生在战斗内，所以效果天然只在战斗内生效。</description></item>
/// <item><description><b>不拒绝"被指定"</b>：迟滞只挡"随机洗牌"，不挡任何主动捞回（本类不拦任何 targeted 效果）。</description></item>
/// <item><description><b>无死锁兜底</b>：若这一批要洗的牌**全是**迟滞牌，则这一次忽略迟滞（否则抽牌堆永远是空的）。</description></item>
/// </list>
/// <para>
/// <b>怎么给一张牌打上迟滞</b>（两条路都生效）：
/// ① **正规路**：卡定义实现标记接口 <see cref="IHysteresisCard" />（词条显示另按"声明 CardKeyword.None"那套做，
/// 见接口注释里说明的槽位限制）；
/// ② **测试路**：<see cref="Mark" />，控制面板「卡组」页有对应按钮。
/// </para>
/// </remarks>
internal static class Hysteresis
{
    private static readonly HashSet<CardModel> Marked = [];

    /// <summary>给一张牌打上迟滞（幂等）。**测试用**：正规做法是在卡定义里声明关键词。</summary>
    public static void Mark(CardModel card)
    {
        if (Marked.Add(card))
        {
            Const.Logger.Info($"[{Const.ModId}] 迟滞：{card.Id.Entry} 已打标（共 {Marked.Count} 张）");
        }
    }

    /// <summary>去掉迟滞标记。</summary>
    public static void Unmark(CardModel card) => Marked.Remove(card);

    /// <summary>
    /// 这张牌带不带迟滞。
    /// </summary>
    /// <remarks>判定 = 卡定义实现了 <see cref="IHysteresisCard" />（卡表的正规路）**或**运行时打过标（测试路）。</remarks>
    public static bool IsStuck(CardModel card) => card is IHysteresisCard || Marked.Contains(card);

    /// <summary>现在有几张带迟滞。</summary>
    public static int Count => Marked.Count;

    /// <summary>清空所有迟滞标记（调试用）。</summary>
    public static void ClearMarks()
    {
        var count = Marked.Count;
        Marked.Clear();
        Const.Logger.Info($"[{Const.ModId}] 迟滞：已清空 {count} 张标记");
    }

    /// <summary>一行状态（面板 / 日志用）。</summary>
    public static string Describe()
        => Marked.Count == 0
            ? "迟滞：没有牌带迟滞"
            : "迟滞：" + string.Join(", ", Marked.Select(card => card.Id.Entry));
}
