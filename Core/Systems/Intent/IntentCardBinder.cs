using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 把一张意图绑成"<b>一张真卡</b>"（供表现层渲成卡面）。
/// </summary>
/// <remarks>
/// ① 职责：以注册类外壳承载每张意图的内容。
/// ② 调用时机：卡带创建卡节点时。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class IntentCardBinder
{
    private static readonly Dictionary<(Creature Owner, IntentCard Card), AeonglassIntentCard> Cache = [];

    /// <summary>取（必要时创建）这张意图对应的真卡实例；失败返回 null（调用方退回外壳）。</summary>
    internal static AeonglassIntentCard? InstanceFor(Player player, Creature owner, IntentCard card)
    {
        if (Cache.TryGetValue((owner, card), out var cached))
        {
            return cached;
        }

        try
        {
            // ⚠️ 必须走本体的"生成一张卡"入口：之前用 `CreateClone()` 在**规范实例**上被拒
            //（`CanonicalModelException: Canonical model ... used in incorrect place`）。
            // 本体自己的 CardFactory 就是这么造的：`player.RunState.CreateCard(model, player)`。
            if (player.RunState.CreateCard(ModelDb.Card<AeonglassIntentCard>(), player)
                is not AeonglassIntentCard model)
            {
                return null;
            }

            var move = IntentCycle.Resolve(owner, card);
            var targets = MonsterAccess.Combat?.PlayerCreatures?.ToList() ?? [];

            model.IntentKey = card.MoveId;
            model.PortraitOverride = PortraitPathOf(move, targets, owner);
            model.NumberText = IntentNumbers.Text(move?.Intents, targets, owner);
            Cache[(owner, card)] = model;
            return model;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 意图卡绑定失败（退回外壳）：{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>战斗结束 / 卡带销毁时清掉（克隆是战斗级的）。</summary>
    internal static void Clear() => Cache.Clear();

    /// <summary>反向找：某个卡模型对应哪张意图（选牌回来的是模型）。</summary>
    internal static IntentCard? IntentOf(CardModel model) =>
        Cache.FirstOrDefault(pair => ReferenceEquals(pair.Value, model)).Key.Card;

    /// <summary>该意图的图路径（本体给的是资源贴图，取它的 <c>ResourcePath</c>；拿不到就 null ⇒ 用占位图）。</summary>
    private static string? PortraitPathOf(
        MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState? move,
        IReadOnlyList<Creature> targets,
        Creature owner)
    {
        foreach (var intent in move?.Intents ?? [])
        {
            try
            {
                var path = intent.GetTexture(targets, owner)?.ResourcePath;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} 取意图图路径失败（跳过这张）：{ex.GetType().Name}");
            }
        }

        return null;
    }

}
