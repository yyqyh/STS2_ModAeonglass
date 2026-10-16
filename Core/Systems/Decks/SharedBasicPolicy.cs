using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Stasis;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 共用池策略：哪些基础牌两副都有一份（孪生的白名单）。
/// </summary>
/// <remarks>
/// ① 职责：判定某张牌是否共用。
/// ② 调用时机：播种与孪生配对。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class SharedBasicPolicy
{
    /// <summary>这张牌违反了 P3-4 吗？违反就返回原因（中文，直接进日志 / 面板）。</summary>
    public static string? ViolationOf(CardModel card)
    {
        // 搁浅（搁浅）：本 mod 自己的接口。
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
            // 2026-10-16：从 Warn 降为 Info —— 这是"数据侧待改"的提示，不是运行时故障，
            // 不该每局刷一条警告。卡表侧（create/output/**）如把它移出共用池，这条会自动消失。
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} P3-4 提示：共用基础牌 {card.Id.Entry} 带了「{reason}」"
                + " —— 打出它会让两副 desync（一张离队、孪生还在）。建议卡表侧把它移出共用池，或明确声明这是例外。");
        }

        if (bad == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} P3-4 审计：共用基础牌没有「离开牌堆」类关键词 ✔");
        }

        return bad;
    }
}
