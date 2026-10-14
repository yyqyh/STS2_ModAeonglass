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
/// <b>播种</b>（CardWTH1，差分 · v2）：2 费 · 两副走**完全不同**的两条路 ——
/// <b>永时副</b>造成 18 伤（升级 24）；<b>消逝副</b>塞 6 张凋萎（升级 8）。
/// </summary>
/// <remarks>v2 重写：v1 的「伤害 + 凋萎各来一点」改成两边各自走到底（伤害 / 污染）。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSowing() : ModCardTemplate(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy),
    IDeckVariantCard
{
    /// <summary>永时副的伤害（升级 +6）。</summary>
    private const decimal DamageA = 18m;

    /// <summary>消逝副塞的凋萎张数（升级 +2）。</summary>
    private const decimal WitherB = 6m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(DamageA, ValueProp.Move), new DynamicVar("WitherB", WitherB)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        if (DeckVariants.SideOf(Owner, this) == DeckSide.B)
        {
            var wither = (int)DynamicVars["WitherB"].BaseValue;
            Const.Logger.Info($"[{Const.ModId}] 播种（消逝副）：{VoidPollution.Inject(cardPlay.Target, wither)}");
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        Const.Logger.Info($"[{Const.ModId}] 播种（永时副）：{DynamicVars.Damage.BaseValue} 伤");
    }

    /// <summary>升级：两副各自的那半边都抬（伤害 +6 / 凋萎 +2）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
        DynamicVars["WitherB"].UpgradeValueBy(2m);
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
