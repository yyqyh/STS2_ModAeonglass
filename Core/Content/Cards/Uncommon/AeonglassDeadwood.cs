using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>枯薪</b>（NEW-37，2026-10-20）· <b>0 费 · Uncommon · 技能</b> ——
/// 将 1 张带[迟滞]的[凋萎]放入手牌；获得 2 费（升级 3 费）。
/// </summary>
/// <remarks>
/// ① 顺序按原文：先放牌，再获能；手牌满时凋萎改放弃牌堆并记一行日志。
/// ② 凋萎是战斗内真卡实例，随迟滞留弃牌堆。
/// ③ 依赖：<c>CombatState.CreateCard</c> + <c>Hysteresis.Mark</c> + <c>PlayerCmd.GainEnergy</c>。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDeadwood()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Skill(0, CardRarity.Uncommon)
            .Var("Energy", 2m, upgradeDelta: 1m))
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combat)
        {
            return;
        }

        var wither = combat.CreateCard(ModelDb.Card<AeonglassWitherCard>(), Owner);
        var full = Owner.PlayerCombatState is { } piles
            && piles.Hand.Cards.Count >= CardPile.MaxCardsInHand;
        await CardPileCmd.Add(wither, full ? PileType.Discard : PileType.Hand);
        Hysteresis.Mark(wither);

        var energy = (int)DynamicVars["Energy"].BaseValue;
        await PlayerCmd.GainEnergy(energy, Owner);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Wither} 枯薪：带迟滞的凋萎 → {(full ? "弃牌堆（手牌已满）" : "手牌")}；+{energy} 费");
    }
}
