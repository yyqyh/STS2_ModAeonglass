using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib;

namespace STS2_Aeonglass.Core.Content.Keywords;

/// <summary>注册 mod 关键词「<b>永恒</b>」（乱流这类牌上的词条）。</summary>
/// <remarks>
/// 写法与「凋萎」「迟滞」「搁浅」一致（<c>RegisterCardKeywordOwnedByLocNamespace</c>），
/// 文案取 <c>card_keywords</c> 表的 <c>STS2_AEONGLASS_KEYWORD_ETERNAL.title / .description</c>。
/// 2026-10-19 核实：RitsuLib 会为每个 mod 关键词生成独立的动态
/// <c>CardKeywordValue</c> ⇒ 卡定义挂 <see cref="Value" /> 即可正确显示「永恒」。
/// </remarks>
internal static class EternalKeyword
{
    /// <summary>本地 stem（id 会变成 <c>STS2_AEONGLASS_KEYWORD_ETERNAL</c>）。</summary>
    internal const string Stem = "eternal";

    /// <summary>RitsuLib 为该关键词生成的动态 <see cref="CardKeyword" /> 值（注册后可用）。</summary>
    internal static CardKeyword Value { get; private set; }

    /// <summary>注册一次（幂等；由 <c>Main.Initialize</c> 调，必须在内容冻结之前）。</summary>
    internal static void Register()
    {
        try
        {
            var definition = RitsuLibFramework
                .GetKeywordRegistry(Const.ModId)
                .RegisterCardKeywordOwnedByLocNamespace(
                    Stem,
                    iconPath: null,
                    cardDescriptionPlacement:
                        STS2RitsuLib.Keywords.ModKeywordCardDescriptionPlacement.AfterCardDescription,
                    includeInCardHoverTip: true);

            Value = definition.CardKeywordValue;

            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Host} 关键词「永恒」已注册：id={definition.Id}"
                + $"（文案 {definition.TitleTable}/{definition.TitleKey}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Host} 关键词「永恒」注册失败（不影响机制）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
