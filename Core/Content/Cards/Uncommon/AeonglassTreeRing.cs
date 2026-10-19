using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>年轮</b>（CardANN1，v2）· <b>0 费 · Uncommon · 技能</b> —— 获得 2 点格挡 × 上回合你打出的牌数（升级 3 点/张）；<b>无上限</b>。
/// </summary>
/// <remarks>
/// ① 效果：格挡 = 上回合出牌数 × 每张点数；升级把每张 2 → 3。
/// ② 差分：无差分。
/// ③ 依赖：<c>CombatHistory.CardsPlayedLastTurn</c>（读已发生的回合）。
/// ④ ⚠️ v2 去掉上限 ⇒ 想加 cap 必须回来加；0 费 + 无上限属"0 费大效果"，强度要盯。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTreeRing() : AeonglassCardTemplate(
    new AeonglassCardSpec(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
        {
            GainsBlock = true,
        }
        .Tag(CardTag.Defend)
        .Var("BlockPerCard", BlockPerCard, upgradeDelta: 1m))
{
    private const decimal BlockPerCard = 2m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var played = CombatHistory.CardsPlayedLastTurn(Owner);
        // v2：把上限去掉了（只由"上回合实际打了几张"决定）⇒ 想改成有上限就必须回来加 cap。
        var block = played * DynamicVars["BlockPerCard"].BaseValue;

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 年轮：上回合打出 {played} 张 ⇒ {block} 挡（v2：无上限）");
        if (block > 0m)
        {
            await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
        }
    }

}
