using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>决堤</b>（CardSD3）：2 费 · 15 伤（+3）；可消耗**最多 2** 沙砾：每点额外 +3 伤。</summary>
/// <remarks>"最多 N"按"有多少花多少（上限 N）"实现（与 SD 通道其余卡同口径，见 <see cref="SandSpend" />）。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBreach() : ModCardTemplate(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    private const decimal BaseDamage = 15m;
    private const int MaxSpend = 2;
    private const decimal PerSpendDamage = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(BaseDamage, ValueProp.Move), new DynamicVar("PerSpendDamage", PerSpendDamage)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var available = SecondaryResourceCmd.Get(Owner, AeonglassSecondaryResources.SandId);
        var toSpend = Math.Min(MaxSpend, Math.Max(0, available));
        var spent = toSpend > 0 && await SecondaryResourceCmd.Spend(
            Owner, AeonglassSecondaryResources.SandId, toSpend);

        var damage = DynamicVars.Damage.BaseValue + (spent ? toSpend * PerSpendDamage : 0m);
        Const.Logger.Info($"[{Const.ModId}] 决堤：消耗 {toSpend} 沙砾 ⇒ {damage} 伤（可用 {available}）");

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}
