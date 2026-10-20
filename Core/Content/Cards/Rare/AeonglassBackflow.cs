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
using STS2_Aeonglass.Core.Systems.Wither;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>倒灌</b>（NEW-33，2026-10-20）· <b>1 费 · Rare · 技能</b> ——
/// 抽 5 张，然后从弃牌堆抽 5 张；将 1 张带[迟滞]的[凋萎]放入弃牌堆。升级：费用 0。
/// </summary>
/// <remarks>
/// ① 效果：两段抽牌（抽牌堆 5 / 弃牌堆 5）+ 一张带运行时迟滞的凋萎。
/// ② 凋萎是**战斗内真卡实例**（<c>CombatState.CreateCard</c>），与玉石俱焚同一条。
/// ③ 依赖：<c>DiscardDraw</c> + <c>AeonglassWitherCard</c> + <c>Hysteresis.Mark</c>。
/// ④ ⚠️ 交接单原文"将张"缺数字，暂按 1 张（已在交接单备注）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBackflow()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Skill(1, CardRarity.Rare)
            .UpgradeEnergy(-1))
{
    private const int WitherCount = 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, 5, Owner);
        await DiscardDraw.DrawAsync(choiceContext, Owner, 5);

        if (Owner.Creature.CombatState is not { } combat)
        {
            return;
        }

        for (var i = 0; i < WitherCount; i++)
        {
            var wither = combat.CreateCard(ModelDb.Card<AeonglassWitherCard>(), Owner);
            await CardPileCmd.Add(wither, PileType.Discard);
            Hysteresis.Mark(wither);
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Wither} 倒灌：{WitherCount} 张带迟滞的凋萎 → 弃牌堆");
    }
}
