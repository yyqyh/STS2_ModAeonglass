using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>定序</b>（INT-5）：1 费 · 把怪物手牌按你选择的顺序全部重排（全面接管）。
/// </summary>
/// <remarks>
/// 打出后进入指向模式，卡带上可行动的意图变可点；点一张 = 那张排到下一位，
/// 每点一下立刻重排手牌并重设它的 <c>NextMove</c>，于是玩家看着卡带一步步排成自己要的顺序。
/// 排完全部可行动的那些之后自动退出指向模式。
/// 虚无不出现在可点集合里（卡带本来就点不了它），但重排时照旧占位。
/// 作用对象 = 场上第一只活着的怪（与同通道的提前 / 跳过 / 剥夺同口径）。升级按卡表不变。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassOrdering() : ModCardTemplate(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Ordering", 1m)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (MonsterAccess.LivingEnemies.FirstOrDefault() is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 定序：场上没有活着的怪");
            return Task.CompletedTask;
        }

        var state = IntentDeck.Ensure(target);
        var total = state.Hand.Count(card => card.Playable);
        if (total == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] 定序：{MonsterAccess.IdOf(target)} 手牌里没有可行动的意图");
            return Task.CompletedTask;
        }

        var picked = new List<IntentCard>();
        IntentTargeting.Begin(Owner, "定序", (_, clicked, card) =>
        {
            if (!ReferenceEquals(clicked, target))
            {
                return $"定序：{MonsterAccess.IdOf(clicked)} 不是本次的目标怪（点 {MonsterAccess.IdOf(target)} 的卡）";
            }

            if (picked.Contains(card))
            {
                return $"定序：{card.MoveId} 已经排过了（点别的）";
            }

            picked.Add(card);

            // 已选前缀 + 其余按当前顺序，每一下都能在卡带上看见进度。
            var rest = state.Hand.Where(handCard => !picked.Contains(handCard)).ToList();
            var text = IntentResolver.Reorder(target, [.. picked, .. rest]);

            if (picked.Count >= total)
            {
                return $"{text}｜定序完成（{picked.Count}/{total}）";
            }

            IntentTargeting.KeepAlive();
            return $"{text}｜已排 {picked.Count}/{total}，继续点";
        });

        Const.Logger.Info(
            $"[{Const.ModId}] 定序：进入多点选取（{MonsterAccess.IdOf(target)} 共 {total} 张可行动意图）");
        return Task.CompletedTask;
    }
}
