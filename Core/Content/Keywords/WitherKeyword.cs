using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;

using STS2RitsuLib;

namespace STS2_Aeonglass.Core.Content.Keywords;

/// <summary>
/// 注册 mod 关键词 **「凋萎」**（用户 2026-10-12：凋萎要加关键词）。
/// </summary>
/// <remarks>
/// <para>
/// 用 RitsuLib 的正规入口：<c>GetKeywordRegistry(modId).RegisterCardKeywordOwnedByLocNamespace(stem)</c>，
/// 它会按 <c>MODID_KEYWORD_&lt;STEM&gt;</c> 生成 id，并去 <c>card_keywords</c> 表里找
/// <c>{id}.title</c> / <c>{id}.description</c>（见 <c>ModKeywordRegistry</c> 的注释）。
/// ⇒ 本 mod 的键就是 <c>STS2_AEONGLASS_KEYWORD_WITHER.title / .description</c>（写在
/// <c>localization/zhs/card_keywords.json</c>）。
/// </para>
/// <para>
/// 注册必须在**内容冻结之前**（mod 初始化阶段）完成，所以由 <c>Main.Initialize</c> 调一次。
/// </para>
/// <para>
/// 2026-10-19：RitsuLib 会为每个 mod 关键词生成独立的动态 <c>CardKeywordValue</c>，
/// 卡定义用 <see cref="Value" /> 挂上即可在卡面/悬停里显示「凋萎」。
/// </para>
/// </remarks>
internal static class WitherKeyword
{
    /// <summary>本地 stem（id 会变成 <c>STS2_AEONGLASS_KEYWORD_WITHER</c>）。</summary>
    internal const string Stem = "wither";

    /// <summary>RitsuLib 为该关键词生成的动态 <see cref="CardKeyword" /> 值（注册后可用）。</summary>
    internal static CardKeyword Value { get; private set; }

    internal static IHoverTip HoverTip => HoverTipFactory.FromKeyword(Value);

    /// <summary>注册一次（幂等；由 <c>Main.Initialize</c> 调）。</summary>
    internal static void Register()
    {
        try
        {
            // 卡片描述注入的匹配规则（已核 RitsuLib 源码）：注入器遍历 card.Keywords，
            // 对每个 CardKeyword 调 TryGetByCardKeyword(...) 找定义。RitsuLib 为每个 mod 关键词
            // 生成独立的动态枚举值（位于原版枚举范围之外），卡定义挂 definition.CardKeywordValue。
            // 位置用 AfterCardDescription：把「凋萎」的词条文本追加到卡描述末尾；悬停提示包含它。
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
                $"[{Const.ModId}] {Const.Prefix.Wither} 关键词「凋萎」已注册：id={definition.Id}（"
                + $"文案 {definition.TitleTable}/{definition.TitleKey}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Wither} 关键词「凋萎」注册失败（不影响机制）："
                + $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
