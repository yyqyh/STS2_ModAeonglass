using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>荒漠化</b>（CardNEW13，delta 20261015）· <b>2 费 · Uncommon · 技能</b> —— 升级<b>所有</b>[凋萎]（敌人三个意图堆里的 + 你自己各牌堆里的）；升级档：<b>费用 −1</b>。
/// </summary>
/// <remarks>
/// ① 效果：遍历敌人意图堆与自己各牌堆，把每张可强化的凋萎升一级；升级让这张牌自己 −1 费。
/// ② 差分：无差分。
/// ③ 依赖：凋萎（<c>UpgradeInternal</c>，0→3 级 ⇒ 掉血 3/4/5/6）+ <c>IntentDeck</c> / <c>MonsterAccess</c>。
/// ④ ⚠️ "包括自己的"＝玩家侧凋萎（2026-10-15 用户新口径，与 凋萎 原口径冲突，见「玉石俱焚」）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDesertification()
    : ModCardTemplate(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var upgraded = UpgradeEnemySide() + UpgradeOwnSide();
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} 荒漠化：升级了 {upgraded} 张凋萎（敌人侧 + 自己侧）");
        return Task.CompletedTask;
    }

    /// <summary>升级档：费用 −1（2 → 1）。</summary>
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    private static int UpgradeEnemySide()
    {
        var upgraded = 0;
        foreach (var enemy in MonsterAccess.LivingEnemies)
        {
            if (!IntentDeck.TryGet(enemy, out var state))
            {
                continue;
            }

            upgraded += UpgradePile(state.Hand);
            upgraded += UpgradePile(state.Discard);
        }

        return upgraded;
    }

    private static int UpgradeOwnSide()
    {
        var upgraded = 0;
        foreach (var player in MonsterAccess.Combat?.Players ?? [])
        {
            if (player.PlayerCombatState is not { } combat)
            {
                continue;
            }

            foreach (var pile in combat.AllPiles)
            {
                foreach (var card in pile.Cards.ToList())
                {
                    if (card is AeonglassWitherCard wither && TryUpgrade(wither))
                    {
                        upgraded++;
                    }
                }
            }
        }

        return upgraded;
    }

    private static int UpgradePile(IEnumerable<IntentCard>? pile)
    {
        var upgraded = 0;
        foreach (var intent in pile ?? [])
        {
            if (intent.WitherCard is { } wither && TryUpgrade(wither))
            {
                upgraded++;
            }
        }

        return upgraded;
    }

    private static bool TryUpgrade(CardModel card)
    {
        if (!card.IsUpgradable)
        {
            return false;
        }

        try
        {
            card.UpgradeInternal();
            return true;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Wither} 荒漠化：升级 {card.Id.Entry} 失败（{ex.GetType().Name}）");
            return false;
        }
    }
}
