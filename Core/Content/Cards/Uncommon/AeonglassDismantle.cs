using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>拆解</b>（INT-4）：1 费 · 查看怪物抽牌堆顶的 3 张（升级 4），把其中 1 张放进它的弃牌堆。
/// </summary>
/// <remarks>
/// 面板开在这只怪的卡带上（真卡横排、点一张即选中，见 <c>MonsterIntentBelt.BeginTopPick</c>）；
/// 点中的那张由 <c>IntentResolver.Dismantle</c> 落进弃牌堆（它会再确认这张还在抽牌堆里）。
/// 降级路径：万一卡带没挂、面板打不开，就自动丢掉顶上第一张 —— 这张牌不该因为表现层缺口变成空过。
/// 作用对象 = 场上第一只活着的怪（与同通道的提前 / 跳过 / 剥夺同口径）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDismantle() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal LookBase = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Look", LookBase)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (MonsterAccess.LivingEnemies.FirstOrDefault() is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 拆解：场上没有活着的怪");
            return Task.CompletedTask;
        }

        var state = IntentDeck.Ensure(target);
        var top = state.Deck.Take((int)DynamicVars["Look"].BaseValue).ToList();
        if (top.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] 拆解：{MonsterAccess.IdOf(target)} 的抽牌堆是空的");
            return Task.CompletedTask;
        }

        var opened = MonsterIntentBeltPresent.BeginTopPick(
            target, top, card => IntentResolver.Dismantle(target, card));

        Const.Logger.Info(
            $"[{Const.ModId}] 拆解：{MonsterAccess.IdOf(target)} 抽牌堆顶 {top.Count} 张（选牌面板={opened}）");

        if (!opened)
        {
            Const.Logger.Warn($"[{Const.ModId}] 拆解：打不开选牌面板，降级为自动丢出顶上第一张");
            Const.Logger.Info($"[{Const.ModId}] 拆解（降级）：{IntentResolver.Dismantle(target, top[0])}");
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Look"].UpgradeValueBy(1m);
}
