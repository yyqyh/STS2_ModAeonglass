using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Curse;

/// <summary>
/// <b>乱流</b>（CardNEW7，v2）· <b>诅咒 · 不可打出 · 诅咒池</b> —— [迟滞][永恒]：它在弃牌堆里时，抽牌堆与弃牌堆<b>整体对调</b>。
/// </summary>
/// <remarks>
/// ① 效果：只要它在（物理）弃牌堆里，本体对两张堆的<b>所有操作全部对调</b>（抽 / 弃 / 洗牌 / 洗回 / 检索 / 计数）；一离开弃牌堆立刻失效，回到弃牌堆立刻重新生效。
/// ② 差分：无差分。
/// ③ 依赖：收口 = 交换 <c>PlayerCombatState</c> 的 <c>&lt;DrawPile&gt;k__BackingField</c> / <c>&lt;DiscardPile&gt;k__BackingField</c>
///    （<c>RiotSwap</c>；写字段而不是挂 getter —— 自动属性 getter 会被 JIT 内联，实测挂 getter 时本体抽/弃/洗一条都不过补丁）；
///    另有"抽取窗口冻结"（<c>RiotDrawWindowPatch</c>：一次抽牌的整个 Task 期间不切换状态，避免最后一张断抽）。
/// ④ ⚠️ 2026-10-14 三次返工（内容交换 → 行为接管 → getter 替换 → <b>字段对调</b>）才通；UI 牌堆按钮仍绑物理堆（按用户口径不碰 UI）。
/// </remarks>
[RegisterCard(typeof(AeonglassTokenCardPool))]
public sealed class AeonglassRiot : ModCardTemplate, IHysteresisCard
{
    public AeonglassRiot()
        : base(-1, CardType.Curse, CardRarity.Curse, TargetType.Self)
    {
    }

    /// <summary>不可打出（本体 <c>IsPlayable</c> 是 protected virtual，覆盖它即可）。</summary>
    protected override bool IsPlayable => false;

    /// <summary>诅咒不显示在卡池奖励里（挂 token 池 ⇒ 拿不到；进牌堆的入口由事件 / 机制给）。</summary>
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => Task.CompletedTask;
}
