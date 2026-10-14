using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>静域</b>（DLY-4，能力）：1 费 · 每回合开始时给怪物手牌里的第一张意图打上迟滞（每场最多 2 次）。
/// </summary>
/// <remarks>
/// 迟滞只拒绝随机洗回，不拒绝任何主动指定（例：剥夺照样能点它）—— 与玩家侧迟滞同口径。
/// 敌人侧迟滞等于永久裁剪招式池：那张意图在弃牌堆里不再参与洗回。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassStillField() : ModCardTemplate(1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    /// <summary>每场最多打几次迟滞（卡表定值 2；每次用掉 1 点额度）。</summary>
    private const decimal Charges = 2m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Charges", Charges)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassStillFieldPower>(
            choiceContext, Owner.Creature, DynamicVars["Charges"].BaseValue, Owner.Creature, this);
}
