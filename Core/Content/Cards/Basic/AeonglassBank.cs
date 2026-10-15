using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// <b>堤岸</b>（CardB4，起始差分 · v2）：1 费 · 基础 6 挡（升级 9）；
/// <b>消逝副</b>：改为 7 挡 + 本回合你受到的伤害<b>延宕 1</b>（升级：延宕 2，挡不变）。
/// </summary>
/// <remarks>
/// <para>
/// 起始牌组的第 10 张（与「潮汐」对称）：永恒副是数值档、消逝副是机制档 ——
/// 两副的起始牌因此各配一张数值牌与一张机制牌（交替强势）。
/// </para>
/// <para>
/// 它是<b>共用池基础牌</b>（两副同名孪生，打出的那一份按「当前在哪一副」决定走哪档），
/// 所以起始注册与 <c>DeckBSeed</c> 两边都要有它。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
[RegisterCharacterStarterCard(typeof(Aeonglass), 1)]
public sealed class AeonglassBank() : ModCardTemplate(1, CardType.Skill, CardRarity.Basic, TargetType.Self),
    IDeckVariantCard
{
    /// <summary>永恒副的基础格挡（升级 +3）。</summary>
    private const decimal BlockA = 6m;

    /// <summary>消逝副的格挡（升级不变）。</summary>
    private const decimal BlockB = 7m;

    /// <summary>消逝副的延宕回合数（升级 +1）。</summary>
    private const decimal DelayBase = 1m;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(BlockA, ValueProp.Move),
        new DynamicVar("VariantBlock", BlockB),
        new DynamicVar("Delay", DelayBase),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (DeckVariants.SideOf(Owner, this) == DeckSide.A)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Move, cardPlay);
            return;
        }

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars["VariantBlock"].BaseValue, ValueProp.Move, cardPlay);
        await ArmDeferralAsync(choiceContext);
    }

    /// <summary>
    /// <b>新增一个延宕实例</b>（规则 5c，2026-10-15 用户口径）：不再撤掉旧实例，直接叠上去 ——
    /// 每个实例各带自己的到期回合与待结算量，各自独立结算；"不可叠加"指的是同一笔伤害不会被二次延宕。
    /// </summary>
    private async Task ArmDeferralAsync(PlayerChoiceContext choiceContext)
        => await PowerCmd.Apply<AeonglassDeferralPower>(
            choiceContext, Owner.Creature, DynamicVars["Delay"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["Delay"].UpgradeValueBy(1m);
    }

    public IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context) => [];

    public string? VariantTextStem(DeckSide side) => side == DeckSide.B ? "b" : "a";

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        DeckVariants.InjectVariantText(description, this);
    }
}
