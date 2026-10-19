using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib;

namespace STS2_Aeonglass.Core.Content.Keywords;

/// <summary>注册 mod 关键词「<b>搁浅</b>」（搁浅，原「滞留」更名后的机制名）。</summary>
/// <remarks>
/// <para>
/// 与「凋萎」「迟滞」同一套写法：<c>RegisterCardKeywordOwnedByLocNamespace(stem)</c>，
/// 文案取 <c>card_keywords</c> 表的 <c>STS2_AEONGLASS_KEYWORD_STASIS.title / .description</c>。
/// </para>
/// <para>
/// ✅ 2026-10-19 核实：RitsuLib 会为每个 mod 关键词生成独立的动态
/// <c>CardKeywordValue</c>（原版枚举范围之外）⇒ 搁浅卡挂 <see cref="Value" /> 即可正确显示「搁浅」。
/// 机制判定仍由 <c>IStasisCard</c> / <c>StasisStore</c> 负责。
/// </para>
/// </remarks>
internal static class StasisKeyword
{
    /// <summary>本地 stem（id 会变成 <c>STS2_AEONGLASS_KEYWORD_STASIS</c>）。</summary>
    internal const string Stem = "stasis";

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
                $"[{Const.ModId}] {Const.Prefix.Stasis} 关键词「搁浅」已注册：id={definition.Id}（"
                + $"文案 {definition.TitleTable}/{definition.TitleKey}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Stasis} 关键词「搁浅」注册失败（不影响机制）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
