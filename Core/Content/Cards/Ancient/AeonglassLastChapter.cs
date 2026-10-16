using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Content.Cards.Ancient;

/// <summary>
/// <b>时之终章</b>（NEW-6）· <b>3 费 · 先古 · 能力</b> —— 将所有牌洗入抽牌堆；回合开始额外获得 3 点能量并抽 5 张。
/// </summary>
/// <remarks>
/// <para>
/// ① 洗入范围（用户 2026-10-16 拍板）：手牌 + 弃牌堆 + 消耗堆 <b>+ 副卡组</b>。
/// 副卡组按"<b>复制进本场</b>"处理（不是搬运）：<c>DeckPiles.CopyInactiveDeckIntoCombat</c> 给每张母本做一份战斗级副本
/// ⇒ 本场两副一起用，战后副本随战斗消失、<c>deck_b</c> 母本一张不动（"结束后正常"）。
/// </para>
/// <para>
/// ② 回合效果（用户 2026-10-16 拍板"每回合"、"额外抽 5 / 额外获得 3 能量"）：由能力
/// <c>AeonglassLastChapterPower</c> 覆写 <c>ModifyHandDraw</c>（本体 BagOfPreparation 同款）与
/// <c>AfterEnergyReset</c>（本体 ArtOfWar 同款）实现 ⇒ 每回合手牌 5+5、能量 +3，而不是"改成本局仅一次"。
/// </para>
/// <para>
/// ③ 稀有度先古 ⇒ 本体自动给先古外观；我们的卡框 provider 对 Ancient 返回 null（不覆盖）⇒ 不需要新美术。
/// </para>
/// <para>
/// ④ 边界：手牌上限（CardPile.MaxCardsInHand）会截断溢出部分（本体自己挡），日志里能看到；
/// 重复打出只刷新同一份能力（效果读常量、不叠层）。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassLastChapter() : ModCardTemplate(3, CardType.Power, CardRarity.Ancient, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState is not { } piles || Owner.Creature.CombatState is not CombatState combat)
        {
            return;
        }

        // ① 副卡组：复制（不是搬运）—— 母本留在 deck_b，副本随本场战斗走。
        var copies = DeckPiles.CopyInactiveDeckIntoCombat(Owner, combat);

        // ② 手牌 / 弃牌堆 / 消耗堆（读属性：乱流生效时以玩家看到的堆为准）+ 上面那批复件 → 抽牌堆。
        var batch = new List<CardModel>();
        batch.AddRange(piles.Hand.Cards);
        batch.AddRange(piles.DiscardPile.Cards);
        batch.AddRange(piles.ExhaustPile.Cards);
        batch.AddRange(copies);

        if (batch.Count > 0)
        {
            await CardPileCmd.Add(batch, PileType.Draw, CardPilePosition.Bottom, null, skipVisuals: false);
        }

        await CardPileCmd.Shuffle(choiceContext, Owner);

        // ③ 回合效果：挂能力（每回合 +5 抽 / +3 能量）。
        await PowerCmd.Apply<AeonglassLastChapterPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 时之终章：手牌 {piles.Hand.Cards.Count} / 弃牌堆 {piles.DiscardPile.Cards.Count}"
            + $" / 消耗堆 {piles.ExhaustPile.Cards.Count} / 副卡组副本 {copies.Count} → 抽牌堆并洗牌");
    }

    /// <summary>升级档：表里"待定" ⇒ 先按不变施工。</summary>
    protected override void OnUpgrade()
    {
    }
}
