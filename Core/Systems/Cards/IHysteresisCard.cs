namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 标记接口：**这张牌带「迟滞」**（洗牌时留在弃牌堆，见 <see cref="Hysteresis" />）。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么用标记接口而不是查关键词</b>（2026-10-13 核出来的限制）：
/// RitsuLib 的 mod 关键词默认都落在**同一个** <c>CardKeyword.None</c> 槽位上（本 mod 的「凋萎」就是这么用的，
/// 见 <c>AeonglassWitherCard.CanonicalKeywords</c>）⇒ **槽位无法区分两个 mod 关键词**，
/// 拿它判"这张卡有没有迟滞"会把凋萎也认成迟滞（而且卡面词条注入也只能映射到其中一个定义）。
/// </para>
/// <para>
/// 所以：**词条显示**走"卡定义声明 <c>CardKeyword.None</c> + 关键词定义"那套（和凋萎一致，视觉上没问题）；
/// **机制判定**走这个标记接口 —— 卡表落地时，带迟滞的卡写上 <c>: IDeckVariantCard…</c> 这类声明即可
/// （例如 <c>public sealed class X : ModCardTemplate, IHysteresisCard</c>）。
/// </para>
/// </remarks>
internal interface IHysteresisCard;
