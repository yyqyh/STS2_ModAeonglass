using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>刻痕</b>（CardANN2）：1 费 · 8 伤（+3）；上回合你每打出 1 张**攻击**牌：+3 伤（最多 +9）。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassNotch() : ModCardTemplate(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const decimal BaseDamage = 8m;
    private const decimal PerAttack = 3m;
    private const decimal CapBase = 9m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(BaseDamage, ValueProp.Move),
        new DynamicVar("PerAttack", PerAttack),
        new DynamicVar("Cap", CapBase),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var attacks = CombatHistory.AttacksPlayedLastTurn(Owner);
        var bonus = Math.Min(attacks * PerAttack, DynamicVars["Cap"].BaseValue);
        var damage = DynamicVars.Damage.BaseValue + bonus;

        Const.Logger.Info($"[{Const.ModId}] 刻痕：上回合攻击 {attacks} 张 ⇒ +{bonus}（合计 {damage}）");
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
