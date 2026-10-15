using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Presentation.Support;
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
    // 用户 2026-10-12：卡牌再大 20%（30×44 → 36×52.8）。
    private const float SlotWidth = 36f;
    private const float SlotHeight = 52.8f;
    private const float SlotGap = 3f;
    private const float RowGap = 6f;

    /// <summary>
    /// **牌堆行的位置**：直接就是它在**卡带**里的坐标（用户 2026-10-12：
    /// "第一个卡牌换了之后卡组位置也变了 —— 挂在卡带上"）。
    /// </summary>
    /// <remarks>
    /// 刻意**不含卡片高度**：以前写成"卡片行底 + 间距"，于是改卡牌大小（`SlotHeight`）时牌堆会跟着跑。
    /// 现在它是相对卡带原点的固定坐标，跟卡牌尺寸解耦 —— 想调就改这两个数：
    /// <c>X</c> = 往右（0 = 与第一张卡左边缘对齐）；<c>Y</c> = 往下（越小越靠上）。
    /// </remarks>
    private static readonly Vector2 PileRowOffset = new(-100f, -50f);

    /// <summary>
    /// **「拆解」选牌面板的位置**：相对卡带原点的固定坐标（与 <see cref="PileRowOffset" /> 同一套口径）。
    /// </summary>
    /// <remarks>X = 往右（0 = 与第一张卡左边缘对齐）；Y = 往下（越小越靠上）。想挪面板就改这两个数。</remarks>
    private static readonly Vector2 TopPickOffset = new(-100f, -170f);

    private Creature? _creature;
    private Control? _anchor;
    private HBoxContainer _row = null!;
    private HBoxContainer _pileRow = null!;
    private Control? _pilePopup;
    private bool? _popupIsDrawPile;
    private Control? _picker;
    private readonly List<AeonglassIntentCardView> _views = [];
    private string _signature = string.Empty;
    private float _timer;

    /// <summary>
    /// "我们选中的"那张意图（青色高光）—— 动作卡进入**指向模式**时由它填上，平时是 null。
    /// </summary>
    /// <remarks>指向模式还没接（下一步）：那时 `<c>SetSelected(card)</c>` 由指向逻辑调，这里只负责画高光。</remarks>
    internal static IntentCard? SelectedCard { get; private set; }

    /// <summary>当前战斗里本地玩家（造意图卡实例要用它的 <c>RunState</c>）。</summary>
    private static MegaCrit.Sts2.Core.Entities.Players.Player? PlayerOf() =>
        MonsterAccess.Combat?.PlayerCreatures?.FirstOrDefault()?.Player;

    /// <summary>指向模式是否打开（打开时意图卡可点；用户 2026-10-12 的"用卡牌选目标"）。</summary>
    internal static bool TargetingActive { get; private set; }

    /// <summary>
    /// 高光口径（用户 2026-10-12）：<b>红 = 怪物将要打出的那张</b>；<b>青 = 我们选中的那张</b>。
    /// </summary>
    private static IntentHighlight HighlightFor(IntentCard card, EnemyIntentState state)
    {
        if (ReferenceEquals(card, state.Pending))
        {
            return IntentHighlight.Pending;
        }

        return ReferenceEquals(card, SelectedCard) ? IntentHighlight.Selected : IntentHighlight.None;
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
        // 让系统层能通知"进/出指向模式"（表现层注册，系统层不反向依赖）。
        IntentBeltBridge.TargetingChanged = SetTargeting;
        // 同一个接缝的另一半：系统层要开"抽牌堆顶选牌"面板时，由这里把请求落到**对应那只怪**的卡带上。
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

    private void Build()
    {
        _row = new HBoxContainer();
        _row.AddThemeConstantOverride("separation", (int)SlotGap);
        AddChild(_row);

        // 抽 / 弃牌堆单独一行，放在**第一张卡正下方**（用户 2026-10-12）。
        _pileRow = new HBoxContainer();
        _pileRow.AddThemeConstantOverride("separation", (int)SlotGap);
        AddChild(_pileRow);
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

        SyncPosition();

        _timer += (float)delta;
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

        var width = _row.GetCombinedMinimumSize().X;
        if (width <= 0f)
        {
            width = SlotWidth;
        }

        var x = _anchor.Position.X + (_anchor.Size.X - width) * 0.5f;
        var y = _anchor.Position.Y - SlotHeight - RowGap;
        Position = new Vector2(x, y);

        // 牌堆行贴在卡片行正下方、与第一张左对齐。
        // 牌堆行：直接挂在卡带上的固定坐标（不再随卡牌尺寸移动）。
        _pileRow.Position = PileRowOffset;
    }

    private void Refresh()
    {
        if (_creature is null)
        {
            return;
        }

        if (!IntentDeck.TryGet(_creature, out var state) || state.Hand.Count == 0)
        {
            ApplySignature("-");
            return;
        }

        var signature = PileSignature("deck", state.Deck) + "|"
                        + string.Join("|", state.Hand.Select(c => $"{c.Describe()}#{c.TimesDrawn}")) + "|"
                        + PileSignature("discard", state.Discard) + "|"
                        // 高光是跟着"怪物将要打出的那张"走的，所以它变了也得重建。
                        + "pending:" + (state.Pending?.MoveId ?? "-")
                        + "|selected:" + (SelectedCard?.MoveId ?? "-")
                        + "|targeting:" + TargetingActive
                        // 用户 2026-10-12：「怪物意图改变也要刷新 UI（重新给予高光）」——
                        // 把判定原文也纳入签名：判定一变就重建整条卡带 ⇒ 高光/灰化都会重新算一遍。
                        + "|decision:" + state.Decision;
        ApplySignature(signature, state);
    }

}
