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

    /// <summary>
    /// 现在有没有"悬停中 / 已确认待取"的意图卡。
    /// </summary>
    /// <remarks>
    /// 瞄准结束那条补丁用它决定"要不要写日志" —— 别的角色、别的卡瞄准时这里为 false，
    /// 于是我们的日志不会漏到别人的局里。
    /// </remarks>
    public static bool HasPending => _hovered is not null || _pending is not null;

    /// <summary>
    /// 侧表里**已经确认**、等着卡牌来取的那一张在不在。
    /// </summary>
    /// <remarks>
    /// 纯诊断用（日志）：和 <see cref="HasPending" /> 的区别是它**不看悬停**，
    /// 所以"瞄准结束但没取到牌"能一眼分辨是"没压到卡"还是"压到了但没提交"。
    /// </remarks>
    public static bool PendingReady => _pending is not null;

    /// <summary>
    /// 现在有没有登记在册的意图卡节点（= 这一场里我们的卡带确实在跑）。
    /// </summary>
    /// <remarks>
    /// 给"瞄准结束"这条补丁做范围门用：没有登记（原版 / 其他 mod / 我们的卡带没开）时
    /// 连返回值包装都不做 ⇒ 本体拿到的还是原来那个 Task。
    /// </remarks>
    public static bool HasNodes => Nodes.Count > 0;

    /// <summary>卡带建卡时登记：这个节点是哪张意图卡。</summary>
    public static void Register(Node node, IntentCard card, Creature owner)
    {
        if (GodotObject.IsInstanceValid(node))
        {
            Nodes[node] = card;
            Owners[node] = owner;
        }
    }

    /// <summary>
    /// 撤销**一个节点**的登记（卡带刷新槽位时用）。
    /// </summary>
    /// <remarks>
    /// ⚠️ 刷新时**不要**用 <see cref="Clear" />：本登记表是**全局的**（所有怪的卡带共用一个），
    /// 每条卡带一刷新就整表清空 ⇒ 别的怪的卡会被当成"不是意图节点"，多点战斗里指向直接失效
    /// （实测：3 只史莱姆的场里点谁的意图都"没有指向到任何意图"）。
    /// 只撤自己那几个节点即可；顺手把自己占用的 hovered 记位清掉。
    /// </remarks>
    public static void Unregister(Node? node)
    {
        if (node is null)
        {
            return;
        }

        Nodes.Remove(node);
        Owners.Remove(node);

        ForgetHovered(node);
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

    /// <summary>这个节点是不是"当前记着的那张"（卡带轮询撤悬停前用它挡一下，别撤掉别的卡带刚上报的那张）。</summary>
    public static bool IsNotedHovered(Node? node) =>
        node is not null && ReferenceEquals(_hovered, node);

    /// <summary>撤掉"记着的悬停"（**只撤这一个节点**；鼠标移开我们这张卡时由卡带轮询调）。</summary>
    public static void ForgetHovered(Node? node)
    {
        if (node is not null && ReferenceEquals(_hovered, node))
        {
            _hovered = null;
        }
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
