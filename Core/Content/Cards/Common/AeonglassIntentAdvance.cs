using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>提前</b>（CardINT1）：0 费 · 把怪物手牌里最后一张可行动的意图提前到队首。</summary>
/// <remarks>
/// 走 M3 已有的动词接口（<see cref="EnemyTimeModule.Advance" /> → <c>IntentResolver.Advance</c>），
/// 与"指向某一张"的机器是同一套实现。当前作用在**场上第一只活着的怪**（与旧测试卡口径一致）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassIntentAdvance() : ModCardTemplate(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (MonsterAccess.LivingEnemies.FirstOrDefault() is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 提前：场上没有活着的怪");
            return Task.CompletedTask;
        }

        var text = EnemyTimeModule.Advance(Owner, target);
        Const.Logger.Info($"[{Const.ModId}] 卡「提前」：{text}");
        return Task.CompletedTask;
    }
}
