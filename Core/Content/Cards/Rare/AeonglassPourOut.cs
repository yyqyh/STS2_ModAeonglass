using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;
using STS2_Aeonglass.Core.Systems.Stasis;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>倾囊</b>（NEW-20）· <b>1 费 · Rare · 技能 · 消耗</b> —— 给予<b>所有</b>手牌「搁浅 1」，并把它们打出去。
/// </summary>
/// <remarks>
/// <para>
/// ① 用户 2026-10-18 口径：每张都用 <c>CardCmd.AutoPlay</c> 真打出去；攻击牌没有目标时打<b>当前第一只活怪</b>；
/// 打不出的牌（不可打出 / 状态）<b>跳过但照样搁浅</b>。
/// </para>
/// <para>
/// ② 顺序：先搁浅母本（离队 + 倒计时），再尝试打出那张战斗副本 —— 所以即使某张打不出去，它也已经搁浅了。
/// </para>
/// <para>
/// ③ 升级：本体关键词「保留」（<c>CardKeyword.Retain</c>）。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassPourOut() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Rare)
        .Keyword(CardKeyword.Exhaust)
        .UpgradeAddKeyword(CardKeyword.Retain))
{
    /// <summary>每张手牌的搁浅场数（台账 grant_strand_all_hand ⇒ 1）。</summary>
    private const int StrandX = 1;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState is not { } piles)
        {
            return;
        }

        var hand = piles.Hand.Cards.Where(card => !ReferenceEquals(card, this)).ToList();
        var stranded = 0;
        var played = 0;

        foreach (var card in hand)
        {
            // ① 先搁浅母本（任意卡都能搁浅：倒计时走 StasisStore 侧表）。
            if (StasisStore.IsReady && card.DeckVersion is { } master)
            {
                card.DeckVersion = null;
                try
                {
                    if (await StasisReturn.SendToZone(Owner, master, StrandX))
                    {
                        stranded++;
                    }
                }
                catch (Exception ex)
                {
                    Const.Logger.Warn(
                        $"[{Const.ModId}] {Const.Prefix.Stasis} 倾囊：搁浅 {master.Id.Entry} 失败（忽略）：{ex.GetType().Name}: {ex.Message}");
                }
            }

            // ② 再尝试把它打出去（不可打出的跳过，但上面已经搁浅了）。
            if (card.Keywords.Contains(CardKeyword.Unplayable))
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 倾囊：{card.Id.Entry} 不可打出 ⇒ 只搁浅、不打出");
                continue;
            }

            var target = card.TargetType == TargetType.AnyEnemy ? MonsterAccess.LivingEnemies.FirstOrDefault() : null;
            try
            {
                await CardPileCmd.Add(card, PileType.Play);
                await CardCmd.AutoPlay(choiceContext, card, target);
                played++;
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Stasis} 倾囊：打出 {card.Id.Entry} 失败（已搁浅，忽略）："
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 倾囊：手牌 {hand.Count} 张 ⇒ 搁浅 {stranded} / 打出 {played}");
    }

}
