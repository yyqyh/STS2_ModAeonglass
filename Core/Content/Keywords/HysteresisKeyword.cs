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
/// ⚠️ <b>已知限制（2026-10-13 核实）</b>：RitsuLib 的 mod 关键词默认都落在同一个
/// <c>CardKeyword.None</c> 槽位上（本 mod 的「凋萎」就是这么用的）⇒
/// <b>槽位无法区分我们的两个 mod 关键词</b>，卡面词条注入只能映射到其中一个定义。
/// 因此机制判定改用标记接口 <c>IHysteresisCard</c>（见其注释）；若将来要两个词条同时正确显示，
/// 得先确认 RitsuLib 是否有"申请独立槽位"的入口。
/// </para>
/// </remarks>
internal static class HysteresisKeyword
{
    /// <summary>本地 stem（id 会变成 <c>STS2_AEONGLASS_KEYWORD_HYSTERESIS</c>）。</summary>
    internal const string Stem = "hysteresis";

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
