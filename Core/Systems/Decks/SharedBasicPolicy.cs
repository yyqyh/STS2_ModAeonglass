using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>P3-4 的守门：**共用池的基础牌不带"离开牌堆 / 牌组"类关键词**。</summary>
/// <remarks>
/// <para>
/// 为什么这条是硬的：共用池的基础牌在两副各有一份实例（孪生）。如果它带"离开牌组"类关键词
/// （搁浅 / 虚无 / 消耗 / 终章），那么打出其中一份就会让它离队，而**孪生还留在另一副**
/// ⇒ 两副立刻 desync，"同一张牌在两副一样"这条前提破掉。
/// </para>
/// <para>
/// 这里**只审计 + 警告，不阻断**：① 卡表还没定稿，拦下来会把测试卡住；
/// ② 真有例外（将来设计上就要"两副各自独立"的牌）由卡表侧明确声明，而不是被这里静默改掉。
/// </para>
/// </remarks>
internal static class SharedBasicPolicy
{
    /// <summary>这张牌违反了 P3-4 吗？违反就返回原因（中文，直接进日志 / 面板）。</summary>
    public static string? ViolationOf(CardModel card)
    {
        // 搁浅（M7）：本 mod 自己的接口。
        if (card is IStasisCard)
        {
            return "搁浅（打出后离开牌组）";
        }

        if (card.Keywords.Contains(CardKeyword.Exhaust))
        {
            return "消耗（Exhaust）";
        }

        if (card.Keywords.Contains(CardKeyword.Ethereal))
        {
            return "虚无（Ethereal）";
        }

        // 「终章」还没实现（规则层挂在待办上）⇒ 现在只留位置：
        // 等它落地，把它的关键词/接口判断加在这里。
        return null;
    }

    /// <summary>逐张审计一批"共用池基础牌"，把违规的打进日志（在孪生配对时调）。</summary>
    /// <returns>违规的张数。</returns>
    public static int Audit(IEnumerable<CardModel> sharedBasics)
    {
        var bad = 0;
        foreach (var card in sharedBasics)
        {
            if (ViolationOf(card) is not { } reason)
            {
                continue;
            }

            bad++;
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} P3-4 违规：共用基础牌 {card.Id.Entry} 带了「{reason}」"
                + " —— 打出它会让两副 desync（一张离队、孪生还在）。请卡表侧把它移出共用池，或明确声明这是例外。");
        }

        if (bad == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} P3-4 审计：共用基础牌没有「离开牌堆」类关键词 ✔");
        }

        return bad;
    }
}
