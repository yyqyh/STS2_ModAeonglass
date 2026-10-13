using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Token;

/// <summary>
/// **通用"意图卡"外壳模型**（用户 2026-10-12 定稿的方案 B 简化版）。
/// </summary>
/// <remarks>
/// <para>
/// 怪物意图卡带要"真实卡牌样式"，而 <c>NCard.Create</c> 必须给一张卡模型 —— 于是注册**这么一张**当外壳，
/// 所有意图共用它；逐张的差异全部在视图层覆盖（见 <c>IntentCardView</c>）：
/// <b>卡图</b> = 意图自己的图，<b>卡面文字</b> = 只剩动态伤害数字，名字/描述/费用/类型全部隐藏。
/// </para>
/// <para>
/// 它挂在**专属 token 池**（<see cref="AeonglassTokenCardPool" />）里，不进角色卡池 ⇒
/// 不会被奖励 / 商店 / 随机生成抽到；也不进起始卡组。
/// </para>
/// <para>0 费、无效果：它永远不可被玩家打出（视图层也不给它交互）。</para>
/// </remarks>
[RegisterCard(typeof(AeonglassTokenCardPool))]
public sealed class AeonglassIntentCard : ModCardTemplate
{
    public AeonglassIntentCard() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override HashSet<CardTag> CanonicalTags => [];

    // ======================= 计划 1 口：让**每个实例**显示自己的内容 =======================
    //
    // 前提（已核）：本体 CardModel 的 Title(110) / PortraitPath(143) 都是 **virtual**，
    // 可以读实例字段 ⇒ 一个注册类就能撑起"每张意图一张真卡"，不必为每个 MoveState 注册一个类。
    // 于是本体那些"认卡"的地方（选牌界面、卡面渲染、拖拽）看到的都是同一套内容 —— 这正是 1/2 口要的。

    /// <summary>这张实例代表哪个意图（由绑定方在克隆后写入；取图/取数字都用它）。</summary>
    internal string IntentKey { get; set; } = string.Empty;

    /// <summary>卡图资源路径（由绑定方写入：就是该意图的图；留空则回退到外壳的占位图）。</summary>
    internal string? PortraitOverride { get; set; }

    /// <summary>卡面上那个数字（由绑定方写入；空 = 不显示）。</summary>
    internal string NumberText { get; set; } = string.Empty;

    /// <summary>不要名字（用户 2026-10-12 口径）。</summary>
    public override string Title => string.Empty;

    /// <summary>卡图 = 该意图的图（拿不到路径就回退到基类那张占位图，绝不返回空路径）。</summary>
    public override string PortraitPath =>
        string.IsNullOrWhiteSpace(PortraitOverride) ? base.PortraitPath : PortraitOverride;

    /// <summary>
    /// 把**这个实例**的数字喂给卡面描述模板（模板在 `cards.json` 里写成 <c>[center]{Number}[/center]</c>）。
    /// </summary>
    /// <remarks>
    /// 卡面的文本都走"本地化模板 + 动态参数"这条路（<c>Description</c> 本身不是 virtual，但本方法是 ✔），
    /// 所以"每张意图显示自己的数字"在**模型层**就能成立 —— 这正是 2 口要的（视图层不再贴文字）。
    /// </remarks>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        // LocString 的参数入口是 Add(name, value)（没有索引器）。
        description.Add("Number", string.IsNullOrEmpty(NumberText) ? " " : NumberText);
    }
}
