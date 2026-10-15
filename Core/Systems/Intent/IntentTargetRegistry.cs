using Godot;

using MegaCrit.Sts2.Core.Entities.Creatures;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图卡<b>目标登记表</b>：原版指向链路拿不到"卡"这个目标，用它旁路。
/// </summary>
/// <remarks>
/// ① 职责：登记/取回本次点中的意图。
/// ② 调用时机：指向回调。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class IntentTargetRegistry
{
    private static readonly Dictionary<Node, IntentCard> Nodes = [];
    private static readonly Dictionary<Node, Creature> Owners = [];

    /// <summary>最近被指向（悬停）的意图卡节点。</summary>
    private static Node? _hovered;

    /// <summary>已经"确定选中"、等着卡牌来取的意图卡。</summary>
    private static IntentCard? _pending;

    /// <summary>
    /// 当前这次瞄准**是不是我们那三张动作卡起的**（用户 2026-10-12：要"只能指向意图卡"）。
    /// </summary>
    /// <remarks>
    /// 由 patch 在"起瞄准"时置起、在"瞄准结束"时清掉；只有它为真时才拒绝生物目标，
    /// 所以**攻击卡瞄准怪物完全不受影响** ✔。
    /// </remarks>
    public static bool AimingOurCard { get; set; }

    /// <summary>卡带建卡时登记：这个节点是哪张意图卡。</summary>
    public static void Register(Node node, IntentCard card, Creature owner)
    {
        if (GodotObject.IsInstanceValid(node))
        {
            Nodes[node] = card;
            Owners[node] = owner;
        }
    }

    /// <summary>卡带重建 / 销毁时清掉（节点已被释放的那些顺手一起清）。</summary>
    public static void Clear()
    {
        Nodes.Clear();
        Owners.Clear();
        _hovered = null;
        _pending = null;
        AimingOurCard = false;
    }

    /// <summary>这个节点是不是我们的意图卡节点。</summary>
    public static bool IsIntentNode(Node? node) =>
        node is not null && GodotObject.IsInstanceValid(node) && Nodes.ContainsKey(node);

    /// <summary>瞄准时记下"最近指向的节点"（由 patch 调）。</summary>
    public static void NoteHovered(Node? node)
    {
        _hovered = IsIntentNode(node) ? node : null;
    }

    /// <summary>玩家确认了这次指向（由 patch 调）：把"最近指向的那张"变成待取的值。</summary>
    public static void CommitHovered()
    {
        if (_hovered is { } node && Nodes.TryGetValue(node, out var card))
        {
            _pending = card;
        }
    }

    /// <summary>卡牌结算时取回"刚被指向的那张"（取一次就清）。</summary>
    public static IntentCard? TakePending()
    {
        var card = _pending;
        _pending = null;
        return card;
    }

    /// <summary>
    /// 把"被点中的意图卡节点"换成**它所属怪的战斗节点**。
    /// </summary>
    /// <remarks>
    /// 这是方案 (a) 的关键一步：本体 <c>NMouseCardPlay.SingleCreatureTargeting</c> 收到"既不是 NCreature
    /// 也不是 NMultiplayerPlayerState"的节点时会**抛异常**；而我们只要把返回值换成那只怪的 <c>NCreature</c>，
    /// 本体那段就照常走通（`target` = 那只怪，对"推迟它的意图"来说语义也对 ✔），
    /// 同时我们把真正的意图卡记进侧表、由卡牌结算时取回（`cardPlay.Target` 不参与）。
    /// </remarks>
    public static Node? SubstituteNode(Node? node)
    {
        if (!IsIntentNode(node))
        {
            return node;
        }

        if (Owners.TryGetValue(node!, out var owner))
        {
            // 先记下"玩家点的是这张意图卡"，再把节点换成那只怪。
            _pending = Nodes[node!];
            return MonsterAccess.NodeOf(owner);
        }

        return node;
    }
}
