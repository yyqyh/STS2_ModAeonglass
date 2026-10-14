using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Ancient;

/// <summary>
/// <b>永世</b>（AN-2，先古）：3 费 · 你的手牌上限 +3，间隙容量 +5（结构成长线的终点：终极容器）。
/// </summary>
/// <remarks>先古牌没有升级档（卡表：升级不变）。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassForever() : ModCardTemplate(3, CardType.Power, CardRarity.Ancient, TargetType.Self)
{
    private const decimal HandLimit = 3m;
    private const decimal GapCapacity = 5m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("HandLimit", HandLimit),
            new DynamicVar("GapCapacity", GapCapacity),
        ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AeonglassForeverPower>(
            choiceContext, Owner.Creature, HandLimit, Owner.Creature, this);

        await PowerCmd.Apply<AeonglassGapCapacityPower>(
            choiceContext, Owner.Creature, GapCapacity, Owner.Creature, this);
    }
}
