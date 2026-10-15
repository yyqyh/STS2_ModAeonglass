using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

using STS2_Aeonglass.Core.Systems.Wither;
namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>撒播</b>（CardWTH2，v2）· <b>0 费 · Common · 技能</b> —— 向目标怪物的弃牌堆塞 2 张凋萎（升级 3 张）。
/// </summary>
/// <remarks>
/// ① 效果：只塞凋萎、不造成伤害；塞进去的牌要等"洗回抽牌堆"才可能被抽到（延迟生效）。
/// ② 差分：无差分。
/// ③ 依赖：凋萎（<c>WitherPool.Inject</c> → 怪物意图卡组）+ 意图 敌人时间；与「播种（消逝副）」「蔓生」「玉石俱焚」共用同一个注入口 ——
///    带「α or β（永时副）」时每次给予凋萎额外 +1 张。
/// ④ ⚠️ 0 费件里属于"大效果"那一类（卡池体检表点名），强度要盯。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBroadcast() : ModCardTemplate(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    private const decimal WitherBase = 2m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Wither", WitherBase)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] 撒播：没有目标怪");
            return Task.CompletedTask;
        }

        var count = (int)DynamicVars["Wither"].BaseValue;
        Const.Logger.Info($"[{Const.ModId}] 撒播：{WitherPool.Inject(target, count)}");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Wither"].UpgradeValueBy(1m);
}
