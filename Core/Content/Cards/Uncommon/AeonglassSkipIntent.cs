using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>跳过</b>（CardINT2，差分）：0 费。
/// <b>永恒副</b> = 把怪物第一张可行动的意图移到队尾；<b>消逝副</b> = 丢弃那张意图并失去 3 点生命。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSkipIntent() : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self),
    IDeckVariantCard
{
    private const decimal VariantHpLoss = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new HpLossVar(VariantHpLoss)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (MonsterAccess.LivingEnemies.FirstOrDefault() is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 跳过：场上没有活着的怪");
            return;
        }

        var side = DeckVariants.SideOf(Owner, this);
        if (side == DeckSide.A)
        {
            Const.Logger.Info($"[{Const.ModId}] 跳过（永恒副）：{EnemyTimeModule.Postpone(Owner, target)}");
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] 跳过（消逝副）：{EnemyTimeModule.Deprive(Owner, target)}");
        await MegaCrit.Sts2.Core.Commands.CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            DynamicVars.HpLoss.BaseValue,
            MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable | MegaCrit.Sts2.Core.ValueProps.ValueProp.Move,
            this,
            cardPlay);
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
