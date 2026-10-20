using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Curse;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>留乱</b>（NEW-41，2026-10-20）· <b>1 费 · Uncommon · 技能 · 保留 · 搁浅 1</b> ——
/// 获得一张[乱流]。升级：费用 0。
/// </summary>
/// <remarks>
/// ① 生成一张真的乱流（诅咒卡）进手牌；手牌满则进弃牌堆。
/// ② 本卡自己搁浅 1 场（打出后离队）。
/// ③ 依赖：<c>AeonglassRiot</c> + <c>CombatState.CreateCard</c> + 搁浅基类。
/// ④ 交接单原文只写"获得一张乱流"⇒ 本批按"进手牌"落实。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassKeepRiot()
    : AeonglassStasisCardTemplate(
        AeonglassCardSpec.Skill(1, CardRarity.Uncommon)
            .Keyword(CardKeyword.Retain)
            .UpgradeEnergy(-1),
        strandCombats: 1)
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combat
            || Owner.PlayerCombatState is not { } piles)
        {
            return;
        }

        var riot = combat.CreateCard(ModelDb.Card<AeonglassRiot>(), Owner);
        var pile = piles.Hand.Cards.Count >= CardPile.MaxCardsInHand
            ? PileType.Discard
            : PileType.Hand;
        await CardPileCmd.Add(riot, pile);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 留乱：乱流 → {pile}");
    }
}
