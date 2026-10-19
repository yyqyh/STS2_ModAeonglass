using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Token;

/// <summary>
/// <b>意图卡外壳</b>（token · 意图 载体）· <b>0 费 · 技能 · token 池</b> —— 给「怪物意图卡带」用的<b>通用</b>卡模型：无效果、永不可被玩家打出。
/// </summary>
/// <remarks>
/// ① 效果：没有效果 —— 它只是 <c>NCard.Create</c> 需要的"一张真卡模型"；逐张差异全部在视图层覆盖（卡图 = 该意图自己的图，卡面只剩动态伤害数字，名字/描述/费用/类型全隐藏）。
/// ② 差分：无差分。
/// ③ 依赖：意图 敌人时间（<c>IntentCard</c> / <c>AeonglassIntentCardView</c>）；挂 <c>AeonglassTokenCardPool</c>（不进角色池 ⇒ 不会被奖励 / 商店 / 随机生成抽到）。
/// ④ ⚠️ 能"一个注册类撑起每张意图一张真卡"的前提是本体的 <c>CardModel.Title / PortraitPath</c> 是 virtual；本体若改这两个成员的可见性，这里会塌。
/// </remarks>
[RegisterCard(typeof(AeonglassTokenCardPool))]
public sealed class AeonglassIntentCard : AeonglassCardTemplate
{
    public AeonglassIntentCard() : base(AeonglassCardSpec.Skill(0, CardRarity.Basic))
    {
    }

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
