using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>先手</b>（CardBA9）：1 费 · 8 伤（+3）；<b>若这是本回合你打出的第一张牌</b>：改为 12 伤（+3）。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassFirstStrike() : AeonglassAttackTemplate(1, CardRarity.Uncommon, 8m, 3m)
{
    private const decimal FirstDamage = 12m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(Damage, ValueProp.Move), new DynamicVar("FirstDamage", FirstDamage)];

    protected override decimal DamageThisPlay
        => CombatHistory.CardsPlayedThisTurn(Owner) == 0 ? FirstDamage + UpgradeDelta : base.DamageThisPlay;

    /// <summary>升级加的那 3 点（"第一张"那一档也跟着涨）。</summary>
    private decimal UpgradeDelta => CurrentUpgradeLevel * 3m;

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["FirstDamage"].UpgradeValueBy(3m);
    }

    protected override Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => Task.CompletedTask;
}
