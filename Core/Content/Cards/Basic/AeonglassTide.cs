using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// <b>潮汐</b>（CardB2，起始差分 · v2）：基础 = 1 费 9 伤；<b>永时副</b>改为 3 伤 + 向怪物弃牌堆塞 2 张凋萎。
/// （升级：基础伤害 +3；永时副的凋萎张数 +1。）
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
[RegisterCharacterStarterCard(typeof(Aeonglass), 1)]
public sealed class AeonglassTide() : AeonglassAttackTemplate(1, CardRarity.Basic, 9m, 3m),
    IDeckVariantCard
{
    /// <summary>永恒副的伤害（定值 3，不随升级变 —— 卡表口径）。</summary>
    private const decimal VariantDamage = 3m;

    /// <summary>永恒副塞几张凋萎（升级 +1）。</summary>
    private const decimal VariantWitherBase = 2m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(Damage, ValueProp.Move),
        new DynamicVar("VariantDamage", VariantDamage),
        new DynamicVar("VariantWither", VariantWitherBase),
    ];

    protected override decimal DamageThisPlay =>
        DeckVariants.SideOf(Owner, this) == DeckSide.A ? VariantDamage : base.DamageThisPlay;

    /// <summary>永恒副：塞凋萎。目标就是被打的那只怪（本卡的攻击目标）。</summary>
    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context)
    {
        if (side != DeckSide.A || context.Play.Target is not { } target)
        {
            return [];
        }

        var count = (int)DynamicVars["VariantWither"].BaseValue;
        return
        [
            new DeckEffectBlock(
                $"wither+{count}",
                _ =>
                {
                    Const.Logger.Info($"[{Const.ModId}] 潮汐（永恒副）：{VoidPollution.Inject(target, count)}");
                    return Task.CompletedTask;
                }),
        ];
    }

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await DeckVariants.PlayVariantAsync(new DeckEffectContext(Owner, this, cardPlay, choiceContext));

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["VariantWither"].UpgradeValueBy(1m);
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
