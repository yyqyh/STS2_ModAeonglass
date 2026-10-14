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

/// <summary><b>因果</b>（CardRW2）：3 费 · 24 伤（+5）；本场每回溯过 1 次：+6 伤（最多 +12）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCausality() : ModCardTemplate(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const decimal BaseDamage = 24m;
    private const decimal PerRewind = 6m;
    private const decimal CapBase = 12m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(BaseDamage, ValueProp.Move),
        new DynamicVar("PerRewind", PerRewind),
        new DynamicVar("Cap", CapBase),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var rewinds = CombatHistory.RewindsThisCombat(Owner);
        var bonus = Math.Min(rewinds * PerRewind, DynamicVars["Cap"].BaseValue);
        var damage = DynamicVars.Damage.BaseValue + bonus;

        Const.Logger.Info($"[{Const.ModId}] 因果：本场回溯 {rewinds} 次 ⇒ +{bonus}（合计 {damage}）");
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5m);
}
