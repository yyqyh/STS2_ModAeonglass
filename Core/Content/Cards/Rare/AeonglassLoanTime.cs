using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>贷时</b>（CardETR3，v2）· <b>1 费 · Rare · 能力</b> —— 获得 1 点[贷时]：每回合开始时可以把<b>下回合的能量</b>提前到本回合使用（下回合相应减少）。
/// </summary>
/// <remarks>
/// ① 效果：打出即挂 <c>AeonglassLoanTimePower</c>（点数 1）；升级 = <b>费用 −1</b>（2 → 1）。
/// ② 差分：无差分。
/// ③ 依赖：跨回合能量管线（<c>AeonglassLoanTimePower</c>）；与「透支 / 预支」同属 ETR 通道（把未来的资源挪到现在）。
/// ④ ⚠️ 口径由实现侧默认："不限点数上限、下回合对应减少"；用户若改口径会追发。升级是"减费"（全池少见）⇒ 价格评估要按打折算。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassLoanTime() : AeonglassPowerCardTemplate(1, CardRarity.Rare)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Borrow", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassLoanTimePower>(
            choiceContext, Owner.Creature, DynamicVars["Borrow"].BaseValue, Owner.Creature, this);

    /// <summary>v2 二次更新：升级是费用 −1。</summary>
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
