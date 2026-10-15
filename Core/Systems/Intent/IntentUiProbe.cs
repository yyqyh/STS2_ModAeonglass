using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 观测器：把"意图 UI 到底刷新了没有"变成可读文本。
/// </summary>
/// <remarks>
/// 本体只渲染 <c>Monster.NextMove.Intents</c>，所以原生意图区的子节点数 == 当前那张卡的图标数。
/// 本轮按**档 1（沿用原版意图图标）**交付，这个探针就是"档 1 有没有刷新"的证据；
/// 档 2（怪物头顶的意图卡带）是可选后续，见交接文档。
/// </remarks>
internal static class IntentUiProbe
{
    public static string Describe(Creature? creature)
    {
        var next = MonsterAccess.NextMoveOf(creature);
        var node = MonsterAccess.NodeOf(creature);
        var intents = next?.Intents ?? [];
        var uiIcons = node?.IntentContainer.GetChildren().OfType<NIntent>().Count() ?? -1;
        return $"nextMove={next?.Id ?? "-"} intents={intents.Count} uiIcons={uiIcons}";
    }

    /// <summary>把当前意图区每个图标的类型与数值打出来（证明刷新不是只改了 id）。</summary>
    public static string DescribeIcons(Creature? creature)
    {
        var next = MonsterAccess.NextMoveOf(creature);
        if (creature is null || next is null)
        {
            return "no intent";
        }

        var targets = MonsterAccess.Combat?.PlayerCreatures ?? [];
        return next.Intents.Count == 0
            ? "(no icon)"
            : string.Join(" | ", next.Intents.Select(i => DescribeIcon(i, targets, creature)));
    }

    /// <summary>取一张卡的图标摘要（与 <c>NIntent</c> / DebugTools 的取法同源）。</summary>
    public static string DescribeIcon(AbstractIntent intent, IEnumerable<Creature> targets, Creature owner)
    {
        try
        {
            var list = targets as IReadOnlyList<Creature> ?? targets.ToList();
            var label = intent.GetIntentLabel(list, owner).GetFormattedText().Trim();
            var texture = intent.GetTexture(list, owner);
            return $"{intent.IntentType}{(texture is null ? "·无图" : string.Empty)}"
                   + (string.IsNullOrWhiteSpace(label) ? string.Empty : $"({label})");
        }
        catch (Exception ex)
        {
            return $"{intent.IntentType}·渲染失败:{ex.GetType().Name}";
        }
    }
}
