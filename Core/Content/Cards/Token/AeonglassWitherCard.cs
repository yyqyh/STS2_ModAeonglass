using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;

namespace STS2_Aeonglass.Core.Content.Cards.Token;

/// <summary>
/// <b>凋萎</b>（凋萎 载体 · token）· <b>0 费 · Status · 不可打出</b> —— 塞进怪物手牌：怪物回合结束后若它还在手牌 ⇒ 你损失 X 点生命，随后它被消耗。
/// </summary>
/// <remarks>
/// ① 效果：它自己不做结算 —— 结算在 凋萎的敌人时间管线里（怪物行动后读 <c>HitPoints</c> 掉血，然后消耗）。
/// ② 差分：无差分。
/// ③ 依赖：意图 敌人时间（<c>IntentCard</c> / <c>IntentTurnFlow</c>）+ 凋萎（<c>WitherPool</c>）；
///    可<b>多次强化</b>：0 → 3 级 ⇒ 掉血 3 / 4 / 5 / 6（<c>MaxUpgradeLevel = 3</c>；「荒漠化」就是按这条升级）。
/// ④ ⚠️ 用户 2026-10-12 定稿八条：只给怪物意图、可被「剥夺」点名、抽/弃牌堆都要显示数量、掉血<b>不回档</b>、
///    统一叫"凋萎"（不再有"虚无"这张卡）、要有自己的关键词、必须是<b>真卡实例</b>（非纯元数据）；
///    2026-10-15 起<b>玩家侧凋萎</b>也开了（「玉石俱焚」往自己弃牌堆塞同一张卡，见该卡注释）。
///    本体属性 = <c>Unplayable</c> + <c>CardType.Status</c>（v2：卡框按状态牌画，「净化」升级档的"所有状态牌"也认它）。
/// </remarks>
[RegisterCard(typeof(AeonglassTokenCardPool))]
public sealed class AeonglassWitherCard : AeonglassCardTemplate
{
    /// <summary>基础掉血（未强化）。</summary>
    public const int BaseHitPoints = 3;

    /// <summary>每强化一级增加的掉血。</summary>
    public const int PerUpgrade = 1;

    /// <summary>
    /// <b>v2：凋萎是「状态牌」</b>（<c>CardType.Status</c>）—— 卡框按状态牌画，
    /// 也让「净化」升级档那句"消耗所有[状态牌]"天然把它包括进去。
    /// </summary>
    public AeonglassWitherCard() : base(
        new AeonglassCardSpec(0, CardType.Status, CardRarity.Basic, TargetType.Self)
            {
                Playable = false,
                MaxUpgradeLevel = 3,
            }
            .Keyword(WitherKeyword.Value)
            .Keyword(CardKeyword.Ethereal))
    {
    }

    /// <summary>这张凋萎当前的掉血量（结算时读它，不再写死 3）。</summary>
    public int HitPoints => BaseHitPoints + PerUpgrade * CurrentUpgradeLevel;

    /// <summary>
    /// 这张卡的两个关键词：<c>None</c>（承载 mod 词条「凋萎」）+ <c>Ethereal</c>（规则 4b 的「虚无」）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>None</c>：RitsuLib 的卡片描述注入器按 <c>TryGetByCardKeyword(card.Keywords 里的枚举值)</c> 匹配，
    /// 而我们的 mod 关键词定义 <c>CardKeywordValue</c> 默认就是 <c>None</c> ⇒ 声明它就能把「凋萎」词条
    /// 挂到这张卡上（卡面高亮 + 悬停说明）。
    /// </para>
    /// <para>
    /// <c>Ethereal</c>（2026-10-14 规则 4b 定稿）：凋萎卡**带虚无关键词** ⇒ 进手牌后回合结束即消耗，
    /// 每张只生效一次。它同时让卡面出现原版的「虚无」词条，与玩法一致。
    /// </para>
    /// </remarks>
    /// <summary>卡面只显示"凋萎：…X 点生命…"，X 随强化实时变（模板在 cards.json 里带 {Wither}）。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Wither", HitPoints.ToString());
    }
}
