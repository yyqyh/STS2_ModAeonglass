using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Content.Keywords;

/// <summary>
/// 按卡面正文自动补 mod 关键词 tips（战斗 / 牌组 / 图鉴共用 <see cref="CardModel.HoverTips" />）。
/// </summary>
/// <remarks>
/// ① 扫描主描述 + 差分 <c>.variant.a/.variant.b</c> 原文，命中「搁浅 / 凋萎 / 迟滞 / 永恒」就挂对应 tip。
/// ② 已经带该关键词的卡跳过（关键词本身已经提供 tip，避免重复）。
/// ③ 纯数据推导，不依赖角色门 ⇒ 主菜单图鉴也能显示。
/// </remarks>
internal static class KeywordHoverTips
{
    public static IReadOnlyList<IHoverTip> For(CardModel card)
    {
        var text = RawText(card);
        var tips = new List<IHoverTip>(4);

        Add(text, "搁浅", StasisKeyword.Value, StasisKeyword.HoverTip, card, tips);
        Add(text, "凋萎", WitherKeyword.Value, WitherKeyword.HoverTip, card, tips);
        Add(text, "迟滞", HysteresisKeyword.Value, HysteresisKeyword.HoverTip, card, tips);
        AddNative(text, "永恒", CardKeyword.Eternal, card, tips);
        AddAny(text, ["空间间隙", "时空裂隙"], MechanicKeywords.SpaceGap.Value, MechanicKeywords.SpaceGap.HoverTip, card, tips);
        Add(text, "坍缩", MechanicKeywords.Collapse.Value, MechanicKeywords.Collapse.HoverTip, card, tips);
        Add(text, "唤回", MechanicKeywords.Recall.Value, MechanicKeywords.Recall.HoverTip, card, tips);
        Add(text, "回溯", MechanicKeywords.Rewind.Value, MechanicKeywords.Rewind.HoverTip, card, tips);
        return tips;
    }

    private static void Add(
        string text,
        string word,
        MegaCrit.Sts2.Core.Entities.Cards.CardKeyword keyword,
        IHoverTip tip,
        CardModel card,
        List<IHoverTip> tips)
    {
        if (text.Contains(word, StringComparison.Ordinal) && !card.Keywords.Contains(keyword))
        {
            tips.Add(tip);
        }
    }

    private static void AddNative(
        string text,
        string word,
        CardKeyword keyword,
        CardModel card,
        List<IHoverTip> tips)
    {
        if (text.Contains(word, StringComparison.Ordinal) && !card.Keywords.Contains(keyword))
        {
            tips.Add(HoverTipFactory.FromKeyword(keyword));
        }
    }

    private static void AddAny(
        string text,
        IReadOnlyList<string> words,
        CardKeyword keyword,
        IHoverTip tip,
        CardModel card,
        List<IHoverTip> tips)
    {
        if (words.Any(word => text.Contains(word, StringComparison.Ordinal))
            && !card.Keywords.Contains(keyword))
        {
            tips.Add(tip);
        }
    }

    private static string RawText(CardModel card)
    {
        var parts = new List<string>();
        try
        {
            parts.Add(card.Description.GetRawText());
        }
        catch
        {
            // token / 未加载文案：忽略
        }

        foreach (var stem in new[] { "a", "b" })
        {
            var key = card.Id.Entry + ".variant." + stem;
            try
            {
                if (LocString.Exists("cards", key))
                {
                    parts.Add(new LocString("cards", key).GetRawText());
                }
            }
            catch
            {
                // 非差分卡 / 缺键：忽略
            }
        }

        return string.Join('\n', parts);
    }
}
