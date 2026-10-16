using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
/// <summary>怪物头顶的「意图卡带」本体：把它的意图手牌画成一条卡带（最多 5 张 + 高光）。</summary>
/// <remarks>
/// ① 职责：建卡带、定时刷新（签名比对）、算高光（红 = 将要打出 / 青 = 我们选中）。
/// ② 调用时机：每场战斗由 <c>AeonglassIntentBeltHost</c> / Manager 挂；之后随刷新间隔跑。
/// ③ 依赖：<c>AeonglassIntentCardView</c>（真卡外壳）+ 意图 意图状态（<c>IntentDeck</c>）+ <c>IntentHighlight</c>。
/// ④ ⚠️ <b>挂点纪律</b>：挂在 <c>IntentContainer</c> 的<b>父节点</b> —— 挂进去会被本体当成 <c>NIntent</c> 处理。
///    牌堆小卡见 <c>.PileChips.cs</c>、顶上选牌见 <c>.TopPick.cs</c>（同一个 partial 类）。
/// </remarks>

public partial class AeonglassIntentBelt : Control
{
    private const int MaxSlots = 5;
    private const float RefreshInterval = 0.2f;

    /// <summary>
    /// "只刷数字"的兜底节奏（秒）。
    /// </summary>
    /// <remarks>
    /// 力量 / 易伤这类加成会让卡面数字变，但意图状态本身没变 ⇒ <c>Revision</c> 与指纹都抓不到。
    /// 所以每 1 s 让可见的几张卡各自重算一次数字（<c>RefreshFace</c> 内部"文本没变就什么都不做"），
    /// 代价是每只怪每秒 ≤5 次小字符串比较。
    /// </remarks>
    private const float NumberRefreshInterval = 1f;
    // 用户 2026-10-12：卡牌再大 20%（30×44 → 36×52.8）。
    private const float SlotWidth = 36f;
    private const float SlotHeight = 52.8f;
    private const float SlotGap = 3f;
    private const float RowGap = 6f;

    /// <summary>牌堆小卡行与卡片行之间的间距（与 <see cref="RowGap" /> 同口径，便于派生落点）。</summary>
    private const float PileRowGap = 6f;

    /// <summary>
    /// **视觉保持量**：卡面定位改成"对齐到槽位"之后，卡面比老位置整体挪了这么多；
    /// 卡带整体补回它 ⇒ 你按老位置调好的东西不用全部重调。
    /// </summary>
    /// <remarks>
    /// <para>派生落点用的也是它：`PileRowOffset / TopPickOffset` 都写成 <c>CardVisualHold + (本地偏移)</c>，
    /// 代码在摆的时候再减掉这一份（见 <c>SyncPosition</c>），所以它们的**实际含义 = 相对卡带原点（= 第一张卡左上角）的偏移**。</para>
    /// <para>把这里改成 <c>(0, 0)</c> ⇒ 卡带按公式落位（会整体往左上跳），落点常量也要跟着重算。</para>
    /// </remarks>
    private static readonly Vector2 CardVisualHold = new(132f, 184.6f);

    /// <summary>
    /// **牌堆行的位置**：直接就是它在**卡带**里的坐标（用户 2026-10-12：
    /// "第一个卡牌换了之后卡组位置也变了 —— 挂在卡带上"）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// **反推口径（2026-10-15 卡面对齐槽位之后）**：卡面现在正好落在槽位里，而槽位就在卡带原点上
    /// ⇒ <b>卡带原点 = 第一张卡的左上角</b>，于是"第一张卡正下方"就是
    /// <c>(0, SlotHeight + 间距)</c>；再加上 <see cref="CardVisualHold" />（代码里会把它减掉）得到本字段的值。
    /// </para>
    /// <para><c>X</c> = 往右（0 = 与第一张卡左边缘对齐）；<c>Y</c> = 往下。</para>
    /// </remarks>
    private static readonly Vector2 PileRowOffset = CardVisualHold + new Vector2(0f, SlotHeight + PileRowGap);

    /// <summary>
    /// 卡带相对"意图行锚点"的**额外落点偏移**（用户 2026-10-15：整体再往左上挪一点、压在怪物意图之上）。
    /// </summary>
    /// <remarks>
    /// X 负往左、Y 负往上；单位与 <c>Position</c> 同口径（锚点所在父节点的局部单位，受相机缩放影响）。
    /// 只影响落点，不影响行宽与槽位大小 —— 想调位置只改这一行。
    /// </remarks>
    private static readonly Vector2 BeltOffset = new(-150f, -200f);

    /// <summary>
    /// **「拆解」选牌面板的位置**：相对卡带原点的固定坐标（与 <see cref="PileRowOffset" /> 同一套口径）。
    /// </summary>
    /// <remarks>
    /// 反推口径同 <see cref="PileRowOffset" />：**卡带原点 = 第一张卡左上角**。
    /// 默认摆在"牌堆小卡行再往下 <c>SlotHeight + PileRowGap</c>"处（不与小卡抢位置）；
    /// X 想跟第一张卡左边缘对齐就填 0，Y 越小越靠上。
    /// </remarks>
    private static readonly Vector2 TopPickOffset =
        CardVisualHold + new Vector2(0f, (SlotHeight + PileRowGap) * 2f);

    private Creature? _creature;
    private Control? _anchor;
    private HBoxContainer _row = null!;
    private HBoxContainer _pileRow = null!;
    private Control? _pilePopup;
    private bool? _popupIsDrawPile;
    private Control? _picker;
    private readonly List<AeonglassIntentCardView> _views = [];
    private Control? _overflowLabel;
    private float _timer;
    private float _numberTimer;

    /// <summary>本卡带登记进 <c>IntentTargetRegistry</c> 的节点（刷新时只撤自己这几个，别清整表）。</summary>
    private readonly List<Node> _registeredNodes = [];

    // ==================== 刷新判定：Revision + 廉价指纹（零字符串、零分配）====================
    //
    // 旧版每 0.2 s 拼一遍"牌堆 + 手牌 + 判定原文"的字符串签名，一变就整条重建。
    // 现在改成：系统层 Revision（每次写状态 +1）+ 这里的整数/引用指纹（漏 bump 时的兜底），
    // 两者任一变化才进入"增量更新"。全部是整数比较与一次 ≤5 项的哈希累加，无字符串、无 LINQ。
    private int _lastRevision = -1;
    private int _lastCounts;
    private int _lastHandHash;
    private object? _lastPendingRef;
    private bool _lastHadState;

    /// <summary>行宽缓存（只在行内容变化后重算一次，不再每帧问容器的最小尺寸）。</summary>
    private float _rowWidth;
    private bool _rowWidthDirty = true;

    /// <summary>锚点（怪身上的意图行）上次的位置/尺寸，用来跳过"没动就不写"的那一帧。</summary>
    private Vector2 _anchorPos;
    private Vector2 _anchorSize;
    private bool _anchorCached;

    /// <summary>一次性诊断：这条卡带是否已经打过"就绪"那行（每场战斗每只怪只打一次）。</summary>
    private bool _loggedReady;

    /// <summary>"空 ↔ 有"切换只在变化时打一行（不是每帧）。</summary>
    private bool? _lastShown;

    /// <summary>当前战斗里本地玩家（造意图卡实例要用它的 <c>RunState</c>）。</summary>
    private static MegaCrit.Sts2.Core.Entities.Players.Player? PlayerOf() =>
        MonsterAccess.Combat?.PlayerCreatures?.FirstOrDefault()?.Player;

    /// <summary>
    /// 高光口径：<b>红 = 怪物将要打出的那张</b>；其余无背光。
    /// </summary>
    /// <remarks>
    /// 2026-10-15：原来还有"青 = 我们选中的那张"，那套指向模式（<c>IntentTargeting` + 卡带点击）实测 0 调用已删除；
    /// 现在真正在用指向的是**本体卡牌选目标那套**（<c>Core/Patches/IntentTargetingPatches.cs</c>），它不依赖这个档位。
    /// </remarks>
    private IntentHighlight HighlightFor(IntentCard card, EnemyIntentState state)
    {
        return ReferenceEquals(card, state.Pending) ? IntentHighlight.Pending : IntentHighlight.None;
    }

    /// <summary>给一只怪的节点挂上卡带（幂等）。</summary>
    public static AeonglassIntentBelt? Ensure(NCreature node, Creature creature)
    {
        var anchor = node.IntentContainer;
        if (anchor is null || anchor.GetParent() is not { } parent)
        {
            return null;
        }

        var name = NodeName(creature);
        if (parent.GetNodeOrNull<AeonglassIntentBelt>(name) is { } existing)
        {
            return existing;
        }

        var belt = new AeonglassIntentBelt
        {
            Name = name,
            _creature = creature,
            _anchor = anchor,
        };

        parent.AddChild(belt);
        // 接缝：系统层要开"抽牌堆顶选牌"面板时，由这里把请求落到**对应那只怪**的卡带上。
        // （另一个接缝 `IntentBeltBridge.TargetingChanged` 的注册随"点击链路"一起删了，见该系统层文件注释。）
        IntentBeltBridge.TopPickRequested ??= FindAndBeginTopPick;
        belt.Build();
        belt.SyncPosition();
        return belt;
    }

    /// <summary>卡带节点名（按生物的 combat id 唯一）。</summary>
    public static string NodeName(Creature creature) => $"AeonglassIntentBelt_{(int)(creature.CombatId ?? 0)}";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    /// <summary>卡带销毁（怪死 / 换房间）时把自己登记过的意图节点撤掉，别在全局登记表里留悬挂节点。</summary>
    public override void _ExitTree()
    {
        UnregisterOwnNodes();
        AbortDeal();
    }

    private void Build()
    {
        _row = new HBoxContainer();
        _row.AddThemeConstantOverride("separation", (int)SlotGap);
        AddChild(_row);

        // 抽 / 弃牌堆单独一行，放在**第一张卡正下方**（用户 2026-10-12）。
        _pileRow = new HBoxContainer();
        _pileRow.AddThemeConstantOverride("separation", (int)SlotGap);
        AddChild(_pileRow);

        // 牌堆行的位置是常量 ⇒ 建好就摆一次，之后不再每帧写。
        _pileRow.Position = PileRowOffset;
        _rowWidthDirty = true;
    }

    public override void _Process(double delta)
    {
        if (_creature is null)
        {
            return;
        }

        if (_creature.IsDead)
        {
            // 怪死了就把卡带一起清掉（战斗房间销毁时也会连带释放）。
            QueueFree();
            return;
        }

        // ⚠️ 这里**不能**用 `IsVisibleInTree()` 当门：手牌一空的那些帧我们会把卡带设成 Visible=false，
        //    而它变回可见恰恰靠下面这段刷新 —— 用自身可见性当门会把自己永久锁死（实测踩到）。
        //    所以只有"战斗 UI 整体不可见"时才跳过定位（战斗房间销毁时整条链会连带释放）。
        var combatUiVisible = MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance?.Ui?.IsVisibleInTree() ?? true;
        if (combatUiVisible)
        {
            SyncPosition();
        }

        _timer += (float)delta;
        _numberTimer += (float)delta;

        // 瞄准期间：自己按鼠标位置命中测试并上报给本体瞄准管理器（拖拽期间 MouseEntered 不来）。
        UpdateTargetingHover();

        // 牌堆小卡：等它挂进树、格子排版出尺寸之后再量（建的时候量不到真实变换）。
        ResolvePendingChips();

        // 发牌特效：每只怪每场战斗一次（触发时机见 AeonglassIntentBelt.Deal 的 Trigger）。
        StepDeal(delta);

        if (_numberTimer >= NumberRefreshInterval)
        {
            _numberTimer = 0f;

            if (IntentDeck.TryGet(_creature, out var state) && state.Hand.Count > 0)
            {
                RefreshFacesOnly(state);
            }
        }

        if (_timer < RefreshInterval)
        {
            return;
        }

        _timer = 0f;
        Refresh();
    }

    /// <summary>贴到意图行上方（同一父节点 ⇒ 坐标同源，自动跟随怪物）。</summary>
    private void SyncPosition()
    {
        if (_anchor is null || !IsInstanceValid(_anchor))
        {
            return;
        }

        // 锚点没动、行宽没脏就直接返回（一帧只做几次 Vector2 比较，不再问容器最小尺寸）。
        if (_anchorCached
            && _anchorPos == _anchor.Position
            && _anchorSize == _anchor.Size
            && !_rowWidthDirty)
        {
            return;
        }

        _anchorPos = _anchor.Position;
        _anchorSize = _anchor.Size;
        _anchorCached = true;

        if (_rowWidthDirty || _rowWidth <= 0f)
        {
            // 行宽只在内容变化后算一次（容器最小尺寸查询要过子节点与字体度量，别每帧做）。
            var width = _row.GetCombinedMinimumSize().X;
            if (width > 0f)
            {
                _rowWidth = width;
                _rowWidthDirty = false;
            }
            else if (_row.GetChildCount() == 0)
            {
                // 行里真的没东西（卡带隐藏中）：没有可量的，也就不必再试。
                _rowWidth = SlotWidth;
                _rowWidthDirty = false;
            }

            // ⚠️ 有子节点但量出 0 = 容器还没排版好 ⇒ **保留 dirty**，下一帧再量。
            //    旧写法在这里直接落 SlotWidth（36）并清掉 dirty ⇒ 行宽被永久锁成 36 ⇒
            //    卡带整体右移半个行宽（实测"整体位置不对"）。
        }

        var x = _anchor.Position.X + (_anchor.Size.X - _rowWidth) * 0.5f + BeltOffset.X;
        var y = _anchor.Position.Y - SlotHeight - RowGap + BeltOffset.Y;
        // 视觉保持：卡面定位改成解析式之后，卡面比"老位置"整体往左上挪了 CardVisualHold 那么多；
        // 加上它 ⇒ 你按老位置调好的卡带不用重调。设成 (0,0) 就按公式算（卡带会整体往左上跳）。
        Position = new Vector2(x, y) + CardVisualHold;

        // 牌堆小卡/预览面板相对卡带原点摆 ⇒ 减掉保持量，它们留在原地（不然会跟着卡带一起跳）。
        if (_pileRow is not null)
        {
            _pileRow.Position = PileRowOffset - CardVisualHold;
        }
    }

    /// <summary>
    /// 指纹：把"会影响这一条卡带显示"的东西压成几个整数/引用比较。
    /// </summary>
    /// <remarks>
    /// <c>Revision</c> 覆盖系统层的每一次写入；这里的计数与手牌哈希是**兜底**（万一某处漏了 Touch）。
    /// 两者任一变化 ⇒ 需要刷新。
    /// </remarks>
    private bool FingerprintChanged(EnemyIntentState? state)
    {
        if (state is null)
        {
            var wasShowing = _lastHadState;
            _lastHadState = false;
            _lastRevision = -1;
            return wasShowing;
        }

        var handHash = 17;
        foreach (var card in state.Hand)
        {
            handHash = unchecked((handHash * 31) + (card.MoveId?.GetHashCode() ?? 0));
        }

        var counts = HashCode.Combine(
            state.Hand.Count, state.Deck.Count, state.Discard.Count, state.Consumed.Count, state.VoidsSpent, state.Reshuffles);

        var changed = !_lastHadState
                      || _lastRevision != state.Revision
                      || _lastCounts != counts
                      || _lastHandHash != handHash
                      || !ReferenceEquals(_lastPendingRef, state.Pending);

        _lastHadState = true;
        _lastRevision = state.Revision;
        _lastCounts = counts;
        _lastHandHash = handHash;
        _lastPendingRef = state.Pending;
        return changed;
    }

    private void Refresh()
    {
        if (_creature is null)
        {
            return;
        }

        var hasState = IntentDeck.TryGet(_creature, out var state) && state.Hand.Count > 0;
        if (!FingerprintChanged(hasState ? state : null))
        {
            return;
        }

        UpdateRow(hasState ? state : null);
    }

    /// <summary>本卡带上一次上报给瞄准管理器的悬停节点（null = 现在没压在我们的卡上）。</summary>
    private Node? _reportedHover;

    /// <summary>
    /// 瞄准期间把"鼠标压在哪张意图卡上"上报给 <c>NTargetManager</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么必须轮询鼠标位置</b>：本体这类卡的瞄准是"按住拖到目标再松开"
    /// （<c>TargetMode.ReleaseMouseToTarget</c>），拖动期间鼠标被被拖的卡节点占住 ⇒ 我们的槽位收不到
    /// <c>MouseEntered</c>，管理器永远拿不到 <c>HoveredNode</c>，一松手就取消
    /// （全历史 log 证据："点中意图卡" 0 次、"没有指向到任何意图（已取消）" 6 次）。
    /// </para>
    /// <para>
    /// 代价：瞄准期间每帧 ≤5 次矩形命中测试 + 一条信号；不在瞄准时第一行就返回。
    /// </para>
    /// </remarks>
    private void UpdateTargetingHover()
    {
        // 只在"正在打我们的指向卡"时动手：别的卡（攻击 / 指向队友 / 自身）的瞄准一点不碰。
        if (MegaCrit.Sts2.Core.Nodes.Combat.NTargetManager.Instance is not { IsInSelection: true } manager
            || !IntentTargetRegistry.AimingOurCard)
        {
            _reportedHover = null;
            return;
        }

        Node? hit = null;
        foreach (var view in _views)
        {
            if (view.Card is { } node && view.ContainsMouse())
            {
                hit = node;
                break;
            }
        }

        if (hit is not null)
        {
            // 每帧重报：管理器那边可能被别的事件清掉 HoveredNode，重报一次最稳（代价只是一条信号）。
            manager.OnNodeHovered(hit);
        }
        else if (_reportedHover is { } previous && IntentTargetRegistry.IsNotedHovered(previous))
        {
            // ⚠️ 只有"管理器现在记着的确实是我们这张"才撤：多条卡带同帧轮询，随便撤会撤掉别的怪刚上报的那张。
            manager.OnNodeUnhovered(previous);
            IntentTargetRegistry.ForgetHovered(previous);
        }

        _reportedHover = hit;
    }
}
