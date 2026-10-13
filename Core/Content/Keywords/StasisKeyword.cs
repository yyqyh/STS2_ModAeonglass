using STS2RitsuLib;

namespace STS2_Aeonglass.Core.Content.Keywords;

/// <summary>注册 mod 关键词「<b>搁浅</b>」（M7，原「滞留」更名后的机制名）。</summary>
/// <remarks>
/// <para>
/// 与「凋萎」「迟滞」同一套写法：<c>RegisterCardKeywordOwnedByLocNamespace(stem)</c>，
/// 文案取 <c>card_keywords</c> 表的 <c>STS2_AEONGLASS_KEYWORD_STASIS.title / .description</c>。
/// </para>
/// <para>
/// ⚠️ <b>暂时不把它挂到卡上</b>（不写进任何卡的 <c>CanonicalKeywords</c>）：mod 关键词默认共用
/// <c>CardKeyword.None</c> 槽位，而 <c>None</c> 已经被「凋萎」占了 —— 再挂一张会让两张卡都显示同一个词条文本。
/// 卡面要写这个词条时，先在卡自己的描述文案里写清楚（等 RitsuLib 支持独立槽位再切过去）。
/// </para>
/// </remarks>
internal static class StasisKeyword
{
    /// <summary>本地 stem（id 会变成 <c>STS2_AEONGLASS_KEYWORD_STASIS</c>）。</summary>
    internal const string Stem = "stasis";

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

            Const.Logger.Info(
                $"[{Const.ModId}] 关键词「搁浅」已注册：id={definition.Id}（"
                + $"文案 {definition.TitleTable}/{definition.TitleKey}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] 关键词「搁浅」注册失败（不影响机制）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}
