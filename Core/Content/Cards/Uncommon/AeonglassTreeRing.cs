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
/// <b>年轮</b>（CardANN1，v2）：0 费 · 获得 2 点格挡 × 上回合你打出的牌数（<b>无上限</b>；升级 3 点/张）。
/// </summary>
/// <remarks>读的是**上回合**（<see cref="CombatHistory.CardsPlayedLastTurn" />）—— 年轮＝读已发生的回合。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTreeRing() : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal BlockPerCard = 2m;

    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("BlockPerCard", BlockPerCard)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var played = CombatHistory.CardsPlayedLastTurn(Owner);
        // v2：把上限去掉了（只由"上回合实际打了几张"决定）⇒ 想改成有上限就必须回来加 cap。
        var block = played * DynamicVars["BlockPerCard"].BaseValue;

        Const.Logger.Info($"[{Const.ModId}] 年轮：上回合打出 {played} 张 ⇒ {block} 挡（v2：无上限）");
        if (block > 0m)
        {
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
        }
    }

    protected override void OnUpgrade() => DynamicVars["BlockPerCard"].UpgradeValueBy(1m);
}
