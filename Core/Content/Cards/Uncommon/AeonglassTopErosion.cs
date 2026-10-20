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
/// <b>顶蚀</b>（NEW-39，2026-10-20）· <b>2 费 · Uncommon · 技能</b> · <b>保留</b> ——
/// 获得 16 点格挡（升级 22），将一张带[迟滞]的[凋萎]放入你的抽牌堆顶。
/// </summary>
/// <remarks>
/// ① 先结算模板的格挡，再放凋萎到抽牌堆顶（替换原顶牌的位置，原牌下移 —— 不是删牌）。
/// ② 凋萎带运行时迟滞（不会在洗牌时被带走）。
/// ③ 依赖：<c>RunSpecEffectsAsync</c> + <c>AeonglassWitherCard</c> + <c>CardPileCmd.Add(Top)</c>。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTopErosion()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Block(2, CardRarity.Uncommon, 16m, 6m)
            .Keyword(CardKeyword.Retain))
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await RunSpecEffectsAsync(choiceContext, cardPlay);

        if (Owner.Creature.CombatState is not { } combat)
        {
            return;
        }

        var wither = combat.CreateCard(ModelDb.Card<AeonglassWitherCard>(), Owner);
        await CardPileCmd.Add(wither, PileType.Draw, CardPilePosition.Top);
        Hysteresis.Mark(wither);

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} 顶蚀：带迟滞的凋萎 → 抽牌堆顶");
    }
}
