using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>摆幅</b>（GRW-3，能力）：1 费 · 你的手牌上限 +1，间隙容量 +2。
/// </summary>
/// <remarks>
/// <para>
/// <b>升级 = 把一点结构从一个轴挪到另一个轴</b>：手牌上限 1 → 2，间隙容量 2 → 1。
/// 两个数常驻在一张牌上，所以它同时挂两个能力（手牌上限一个、间隙容量一个），各自读各自的数。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSwingFrame() : ModCardTemplate(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal HandLimitBase = 1m;
    private const decimal GapCapacityBase = 2m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("HandLimit", HandLimitBase),
            new DynamicVar("GapCapacity", GapCapacityBase),
        ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AeonglassSwingFramePower>(
            choiceContext, Owner.Creature, DynamicVars["HandLimit"].BaseValue, Owner.Creature, this);

        await PowerCmd.Apply<AeonglassGapCapacityPower>(
            choiceContext, Owner.Creature, DynamicVars["GapCapacity"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HandLimit"].UpgradeValueBy(1m);
        DynamicVars["GapCapacity"].UpgradeValueBy(-1m);
    }
}
