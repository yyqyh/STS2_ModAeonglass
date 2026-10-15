using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图 UI 观测器：把"意图刷新了没有"变成可读文本。
/// </summary>
/// <remarks>
/// ① 职责：只读探针。
/// ② 调用时机：调试与控制面板。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
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
