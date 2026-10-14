using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>播种</b>（CardWTH1，差分）：3 费。
/// <b>永恒副</b>：10 伤 + 塞 3 张凋萎；<b>消逝副</b>：14 伤 + 塞 2 张凋萎。（升级：各自伤害 +3。）
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSowing() : ModCardTemplate(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy),
    IDeckVariantCard
{
    private const decimal DamageA = 10m;
    private const decimal DamageB = 14m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(DamageA, ValueProp.Move),
        new DynamicVar("VariantDamage", DamageB),
        new DynamicVar("WitherA", 3m),
        new DynamicVar("WitherB", 2m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var sideB = DeckVariants.SideOf(Owner, this) == DeckSide.B;
        var damage = sideB ? DamageB + UpgradeDelta : DynamicVars.Damage.BaseValue;
        var wither = (int)(sideB ? DynamicVars["WitherB"].BaseValue : DynamicVars["WitherA"].BaseValue);

        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        Const.Logger.Info(
            $"[{Const.ModId}] 播种（{(sideB ? "消逝副" : "永恒副")}）：{VoidPollution.Inject(cardPlay.Target, wither)}");
    }

    private decimal UpgradeDelta => CurrentUpgradeLevel * 3m;

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
