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
/// ⚠️ 待接的一步：把这条关键词**绑定到凋萎卡**上（让它在卡面/悬停里成为可高亮、可悬停的词条）。
/// 现在卡自己的描述已经把规则写清了；绑定入口在 RitsuLib 的
/// <c>ModKeywordCardDescriptionPlacement</c> / <c>CardKeywordValue</c>（把 mod 关键词映射到某个
/// <c>CardKeyword</c> 枚举槽位上），下一步核清参数再接。
/// </para>
/// </remarks>
internal static class WitherKeyword
{
    /// <summary>本地 stem（id 会变成 <c>STS2_AEONGLASS_KEYWORD_WITHER</c>）。</summary>
    internal const string Stem = "wither";

    /// <summary>注册一次（幂等；由 <c>Main.Initialize</c> 调）。</summary>
    internal static void Register()
    {
        try
        {
            // 卡片描述注入的匹配规则（已核 RitsuLib 源码）：注入器遍历 card.Keywords，
            // 对每个 CardKeyword 调 TryGetByCardKeyword(...) 找定义 ⇒ 我们用一个**原版没在用**的槽位
            // （CardKeyword.None，恰好也是定义里 CardKeywordValue 的默认值）来挂，避免劫持 Exhaust/Eternal 等语义。
            // 位置用 AfterCardDescription：把「凋萎」的词条文本追加到卡描述末尾；悬停提示包含它。
            var definition = RitsuLibFramework
                .GetKeywordRegistry(Const.ModId)
                .RegisterCardKeywordOwnedByLocNamespace(
                    Stem,
                    iconPath: null,
                    cardDescriptionPlacement:
                        STS2RitsuLib.Keywords.ModKeywordCardDescriptionPlacement.AfterCardDescription,
                    includeInCardHoverTip: true);

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
