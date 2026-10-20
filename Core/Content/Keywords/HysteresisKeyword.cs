using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;

using STS2RitsuLib;

namespace STS2_Aeonglass.Core.Content.Keywords;

/// <summary>注册 mod 关键词「<b>迟滞</b>」（规则层 v1.1 的新关键词）。</summary>
/// <remarks>
/// <para>
/// 与「凋萎」同一套写法：<c>GetKeywordRegistry(modId).RegisterCardKeywordOwnedByLocNamespace(stem)</c>，
/// 文案取 <c>card_keywords</c> 表的 <c>STS2_AEONGLASS_KEYWORD_HYSTERESIS.title / .description</c>。
/// </para>
/// <para>
/// <b>这一层只管"显示成词条"</b>；真正拦洗牌的判定在 <c>Core/Systems/Cards/Hysteresis.cs</c>（标记表）
/// 与 <c>AeonglassCore.ModifyShuffleOrder</c>（执行）。
/// </para>
/// <para>
/// ✅ 2026-10-19 核实：RitsuLib 会为每个 mod 关键词生成独立的动态
/// <c>CardKeywordValue</c>（原版枚举范围之外）⇒ 卡定义挂 <see cref="Value" /> 即可正确显示「迟滞」。
/// 机制判定仍用标记接口 <c>IHysteresisCard</c>（见其注释），两者互不替代。
/// </para>
/// </remarks>
internal static class HysteresisKeyword
{
    /// <summary>本地 stem（id 会变成 <c>STS2_AEONGLASS_KEYWORD_HYSTERESIS</c>）。</summary>
    internal const string Stem = "hysteresis";

    /// <summary>RitsuLib 为该关键词生成的动态 <see cref="CardKeyword" /> 值（注册后可用）。</summary>
    internal static CardKeyword Value { get; private set; }

    internal static IHoverTip HoverTip => HoverTipFactory.FromKeyword(Value);

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
                $"[{Const.ModId}] {Const.Prefix.Host} 关键词「迟滞」已注册：id={definition.Id}（"
                + $"文案 {definition.TitleTable}/{definition.TitleKey}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Host} 关键词「迟滞」注册失败（不影响机制）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
