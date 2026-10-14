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

/// <summary><b>年轮</b>（CardANN1）：0 费 · 获得 2 点格挡 × 上回合你打出的牌数（最多 8；升级上限 +2）。</summary>
/// <remarks>读的是**上回合**（<see cref="CombatHistory.CardsPlayedLastTurn" />）—— 年轮＝读已发生的回合。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTreeRing() : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal BlockPerCard = 2m;
    private const decimal CapBase = 8m;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("BlockPerCard", BlockPerCard), new DynamicVar("Cap", CapBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var played = CombatHistory.CardsPlayedLastTurn(Owner);
        var cap = DynamicVars["Cap"].BaseValue;
        var block = Math.Min(played * BlockPerCard, cap);

        Const.Logger.Info($"[{Const.ModId}] 年轮：上回合打出 {played} 张 ⇒ {block} 挡（上限 {cap}）");
        if (block > 0m)
        {
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Cap"].UpgradeValueBy(2m);
}
