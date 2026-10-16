using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Intent;

using STS2_Aeonglass.Core.Systems.Wither;
namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>玉石俱焚</b>（CardNEW12，delta 20261015）· <b>0 费 · Uncommon · 技能</b> —— 将 6 张[凋萎]放入敌人弃牌堆；将 4 张[凋萎]放入<b>自己</b>弃牌堆（升级 12 / 8）。
/// </summary>
/// <remarks>
/// ① 效果：敌人侧走 凋萎 注入口；自己侧把同一张不可打出的凋萎卡实例塞进自己的弃牌堆（占牌位）。
/// ② 差分：无差分。
/// ③ 依赖：<c>WitherEffects.Inject</c>（敌人侧）+ <c>RunState.CreateCard</c> / <c>CardPileCmd.Add</c>（自己侧）。
/// ④ ⚠️ 玩家侧凋萎是 2026-10-15 用户新开的（凋萎 原口径"只存在于怪物手牌"被推翻）⇒ 它那句"掉血"只在怪物手牌里生效，在自己牌堆里是纯堵塞；要有惩罚只改 <c>PutIntoOwnDiscardAsync</c> 一处。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassMutualRuin()
    : ModCardTemplate(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const decimal ToEnemyBase = 6m;
    private const decimal ToSelfBase = 4m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("ToEnemy", ToEnemyBase), new DynamicVar("ToSelf", ToSelfBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var toEnemy = (int)DynamicVars["ToEnemy"].BaseValue;
        var toSelf = (int)DynamicVars["ToSelf"].BaseValue;

        if (cardPlay.Target is { } target)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} 玉石俱焚：{WitherEffects.Inject(target, toEnemy)}");
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} 玉石俱焚：自己弃牌堆 +{await PutIntoOwnDiscardAsync(Owner, toSelf)} 张凋萎");
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ToEnemy"].UpgradeValueBy(ToEnemyBase);
        DynamicVars["ToSelf"].UpgradeValueBy(ToSelfBase);
    }

    /// <summary>把 N 张真卡实例的凋萎放进自己的弃牌堆（走本体 <c>CardPileCmd.Add</c> ⇒ 有正常移动表现）。</summary>
    private static async Task<int> PutIntoOwnDiscardAsync(Player player, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var model = ModelDb.Card<AeonglassWitherCard>();
        // ⚠️ 必须用 CombatState.CreateCard（登记进本次战斗），不能用 RunState.CreateCard：
        //    后者只登记进 run，这张卡一进战斗牌堆（或之后被「净化 / 时间主宰」消耗）就会抛
        //    "must be added to a CombatState before adding it to this pile"（2026-10-16 由 headless 测试抓到）。
        if (player.Creature.CombatState is not { } combat)
        {
            return 0;
        }

        var placed = 0;
        for (var i = 0; i < count; i++)
        {
            try
            {
                var card = combat.CreateCard(model, player);
                await CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Bottom);
                placed++;
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Wither} 玉石俱焚：自己这侧第 {i + 1} 张凋萎创建失败（{ex.GetType().Name}: {ex.Message}）");
                break;
            }
        }

        return placed;
    }
}
