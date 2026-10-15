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
/// <b>时枢</b>（CardUTL6，v2）· <b>0 费 · Rare · 能力</b> —— 每回合开始时获得 1 点能量；你的手牌上限 −1。
/// </summary>
/// <remarks>
/// ① 效果：打出即挂 <c>AeonglassTimeHubPower</c>（点数 1）；升级不变。
/// ② 差分：无差分。
/// ③ 依赖：每回合能量（能力的回合开始钩子）+ 手牌上限（RitsuLib <c>IMaxHandSizeModifier</c>，挂在 <c>AeonglassHandLimitPower</c> 基类上）。
/// ④ ⚠️ 0 费换"每回合 +1 能量"，卡池体检表把它列进"0 费大效果"那一类 ⇒ 强度要盯（手牌上限 −1 是唯一的代价）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTimeHub() : AeonglassPowerCardTemplate(0, CardRarity.Rare)
{
    private const decimal TurnEnergyBase = 1m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("TurnEnergy", TurnEnergyBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassTimeHubPower>(
            choiceContext, Owner.Creature, DynamicVars["TurnEnergy"].BaseValue, Owner.Creature, this);
}
