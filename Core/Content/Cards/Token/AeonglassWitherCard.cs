using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Token;

/// <summary>
/// **凋萎**（M5）：塞进**怪物手牌**的不可打出卡；怪物回合结束后，若它还在手牌里 ⇒ 你损失 X 点生命，随后它被消耗。
/// </summary>
/// <remarks>
/// <para>
/// 用户 2026-10-12 定稿：① 只给怪物意图（没有玩家侧凋萎）；② 可以被「剥夺」点名；③ 抽/弃牌堆都要显示它的数量；
/// ④ **可强化、且可多次强化**（基础 3 点，每级 +1 ⇒ 3/4/5/6）；⑤ 掉血**不回档**；⑥ 不再有"虚无"这张卡，
/// 一律叫**凋萎**；⑦ 要有自己的关键词/说明；⑧ 要是**真卡实例**（不是纯元数据）。
/// </para>
/// <para>
/// 本体属性 = <c>Unplayable</c>（不可打出）+ 我们自己的数值；挂在 token 池，不进角色卡池、不进起始卡组。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassTokenCardPool))]
public sealed class AeonglassWitherCard : ModCardTemplate
{
    /// <summary>基础掉血（未强化）。</summary>
    public const int BaseHitPoints = 3;

    /// <summary>每强化一级增加的掉血。</summary>
    public const int PerUpgrade = 1;

    /// <summary>
    /// <b>v2：凋萎是「状态牌」</b>（<c>CardType.Status</c>）—— 卡框按状态牌画，
    /// 也让「净化」升级档那句"消耗所有[状态牌]"天然把它包括进去。
    /// </summary>
    public AeonglassWitherCard() : base(0, CardType.Status, CardRarity.Basic, TargetType.Self)
    {
    }

    /// <summary>不可打出（本体 <c>IsPlayable</c> 是 protected virtual，覆盖它即可）。</summary>
    protected override bool IsPlayable => false;

    /// <summary>可**多次**强化（用户 2026-10-12）：0 → 3 级 ⇒ 掉血 3 / 4 / 5 / 6。</summary>
    public override int MaxUpgradeLevel => 3;

    /// <summary>这张凋萎当前的掉血量（结算时读它，不再写死 3）。</summary>
    public int HitPoints => BaseHitPoints + PerUpgrade * CurrentUpgradeLevel;

    protected override HashSet<CardTag> CanonicalTags => [];

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
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.None, CardKeyword.Ethereal];

    /// <summary>卡面只显示"凋萎：…X 点生命…"，X 随强化实时变（模板在 cards.json 里带 {Wither}）。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Wither", HitPoints.ToString());
    }
}
