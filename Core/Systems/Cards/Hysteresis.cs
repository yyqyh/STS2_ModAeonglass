using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>「迟滞」：带这个关键词的牌<b>不参与洗牌</b>（洗牌时跳过、留在弃牌堆）。</summary>
/// <remarks>
/// ① 职责：<c>Mark</c> 打标记 + 判定"洗牌时这张要不要跳过"。
/// ② 挂点与时机：<c>AeonglassCore.ModifyShuffleOrder</c> 执行；控制面板「卡组」页有测试按钮。
/// ③ 依赖：<see cref="IHysteresisCard" />（正规路声明）、卡关键词与 <c>static_hover_tips</c> 文案。
/// ④ ⚠️ 规则层 v1.1 口径：战斗内限定（洗牌只发生在战斗内）；只挡"随机洗牌"、不挡任何主动捞回；
/// 若这一批要洗的牌<b>全是</b>迟滞牌，则这一次忽略迟滞（无死锁兜底，否则抽牌堆永远是空的）。
/// 打标记两条路都生效：卡定义实现 <see cref="IHysteresisCard" />（正规，词条槽位限制见接口注释）/ <c>Mark</c>（测试）。
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
