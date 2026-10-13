using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>双刃</b>（CardBA2）：1 费 · 9 伤 · 你失去 1 点生命。</summary>
/// <remarks>
/// "失去生命"用 <see cref="ValueProp.Unblockable" />（本体注释：*Unblockable = HP loss like Poison*），
/// 与本体「放血」同款写法；掉血数值走 <see cref="HpLossVar" />，所以卡面能显示 `{HpLoss}`。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTwinBlade() : AeonglassAttackTemplate(1, CardRarity.Common, 9m, 3m)
{
    /// <summary>自己掉多少血（固定 1，不随升级变 —— 卡表口径）。</summary>
    private const decimal SelfHpLoss = 1m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(Damage, ValueProp.Move), new HpLossVar(SelfHpLoss)];

    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Move,
            this,
            cardPlay);
}
