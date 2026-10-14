using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Presentation.UI.IntentBelt;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// 档 2：怪物头顶的「意图卡带」——把它的意图手牌画成一条卡带（只读）。
/// </summary>
/// <remarks>
/// <para>
/// 形态：每张 = 一个自绘小卡（空槽卡框 + 意图图标 + 角标），最多显示 <see cref="MaxSlots" /> 张，
/// 多出来的用 "…+N" 收尾（Lab C 实测手牌能被灌到 22 张）。
/// </para>
/// <para>
/// <b>挂点纪律（实测坑）</b>：卡带挂在 <c>IntentContainer</c> 的<b>父节点</b>上，
/// <b>绝不能挂进 <c>IntentContainer</c> 内部</b> —— 本体那段代码把它的子节点**全部当成 <c>NIntent</c>**
/// （<c>GetChild&lt;NIntent&gt;(i)</c> + <c>TakeLast(childCount - i)</c>），混进去会类型转换失败，
/// 甚至被当成多余意图释放掉。挂在父节点上同样能自动跟随怪物（坐标与意图区同源）。
/// </para>
/// <para>与旧版意图图标的关系：卡带放在意图行**上方**，原版图标照常显示（当初的"二选一"两个都不冲突）。</para>
/// </remarks>
public partial class MonsterIntentBelt : Control
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
    private readonly List<IntentCardView> _views = [];
    private string _signature = string.Empty;
    private float _timer;

    /// <summary>
    /// "我们选中的"那张意图（青色高光）—— 动作卡进入**指向模式**时由它填上，平时是 null。
    /// </summary>
    /// <remarks>指向模式还没接（下一步）：那时 `<c>SetSelected(card)</c>` 由指向逻辑调，这里只负责画高光。</remarks>
    internal static IntentCard? SelectedCard { get; private set; }

    /// <summary>设置"我们选中的"那张意图（青色高光）。传 null 清掉。</summary>
    internal static void SetSelected(IntentCard? card) => SelectedCard = card;

    /// <summary>当前战斗里本地玩家（造意图卡实例要用它的 <c>RunState</c>）。</summary>
    private static MegaCrit.Sts2.Core.Entities.Players.Player? PlayerOf() =>
        MonsterAccess.Combat?.PlayerCreatures?.FirstOrDefault()?.Player;

    /// <summary>指向模式是否打开（打开时意图卡可点；用户 2026-10-12 的"用卡牌选目标"）。</summary>
    internal static bool TargetingActive { get; private set; }

    /// <summary>切指向模式（由系统层通过 <c>MonsterIntentBeltPresent</c> 调；每 0.2s 的刷新会跟上）。</summary>
    internal static void SetTargeting(bool on)
    {
        TargetingActive = on;
        SelectedCard = null;
    }

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
    public static MonsterIntentBelt? Ensure(NCreature node, Creature creature)
    {
        var anchor = node.IntentContainer;
        if (anchor is null || anchor.GetParent() is not { } parent)
        {
            return null;
        }

        var name = NodeName(creature);
        if (parent.GetNodeOrNull<MonsterIntentBelt>(name) is { } existing)
        {
            return existing;
        }

        var belt = new MonsterIntentBelt
        {
            Name = name,
            _creature = creature,
            _anchor = anchor,
        };

        parent.AddChild(belt);
        // 让系统层能通知"进/出指向模式"（表现层注册，系统层不反向依赖）。
        MonsterIntentBeltPresent.TargetingChanged = SetTargeting;
        // 同一个接缝的另一半：系统层要开"抽牌堆顶选牌"面板时，由这里把请求落到**对应那只怪**的卡带上。
        MonsterIntentBeltPresent.TopPickRequested ??= FindAndBeginTopPick;
        belt.Build();
        belt.SyncPosition();
        return belt;
    }

    /// <summary>
    /// 接缝实现：按节点名找到 <paramref name="creature" /> 的卡带，把选牌面板开在它身上。
    /// </summary>
    /// <remarks>找不到那只怪的卡带（怪死了 / 还没挂）就返回 false —— 卡那一侧会按"没选到"处理。</remarks>
    private static bool FindAndBeginTopPick(
        Creature creature,
        IReadOnlyList<IntentCard> cards,
        Func<IntentCard, string> onPick)
    {
        var anchor = MonsterAccess.NodeOf(creature)?.IntentContainer;
        if (anchor?.GetParent() is not { } parent)
        {
            return false;
        }

        var belt = parent.GetNodeOrNull<MonsterIntentBelt>(NodeName(creature));
        return belt is not null && belt.BeginTopPick(cards, onPick);
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

    private static string PileSignature(string tag, IReadOnlyList<IntentCard> pile) =>
        tag + ":" + string.Join(",", pile.Select(c => c.MoveId));

    private void ApplySignature(string signature, EnemyIntentState? state = null)
    {
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;

        // ⚠️ 关键：把旧卡**还给对象池之前**先收掉高光/灰化/放大 —— 否则池里那张卡带着
        // `CardHighlight` 亮着就被下一格复用，表现就是"青色高光赖着不消"（用户 2026-10-12 报的）。
        foreach (var view in _views)
        {
            view.SetHighlight(IntentHighlight.None);
            view.SetDim(false);
        }

        _views.Clear();
        IntentTargetRegistry.Clear();

        foreach (var child in _row.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var child in _pileRow.GetChildren())
        {
            child.QueueFree();
        }

        if (state is null)
        {
            Visible = false;
            return;
        }

        Visible = true;

        var targets = MonsterAccess.Combat?.PlayerCreatures ?? [];
        var shown = 0;

        // 牌堆行（第一张卡正下方）：抽 / 弃 两格，用**玩家牌堆那种**样式。
        // 诊断（用户 2026-10-12：「改变意图没有『显示』将要打出的那张牌」）：
        // 把这一轮重建时认定的 pending、手牌顺序、判定原文一起打出来，跟卡面高光一对就能定位。
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Intent} 卡带重建：手牌 {state.Hand.Count} 张 "
            + $"[{string.Join(",", state.Hand.Select(c => c.MoveId))}]｜"
            + $"pending={(state.Pending?.MoveId ?? "-")}｜判定={state.Decision}");

        AddPileIndicator(isDraw: true, "抽", state.Deck);
        AddPileIndicator(isDraw: false, "弃", state.Discard);

        // 预览面板如果开着 ⇒ 用**当前**内容重建一次。
        // 这既修"自动修改后没刷新"，也修"关掉再点还是旧内容"（每次重建时面板都跟着重算）。
        if (_popupIsDrawPile is { } openKind)
        {
            ShowPilePopup(openKind ? "抽" : "弃", openKind ? state.Deck : state.Discard, _pileRow);
        }

        foreach (var card in state.Hand)
        {
            if (shown >= MaxSlots)
            {
                break;
            }

            // 真卡外壳（用户 2026-10-12 口径：卡图用意图的、文字只有动态伤害数字）。
            // 模型驱动：卡图由"这张意图对应的真卡实例"给出（计划 1/2 口）。
            var view = IntentCardView.Create(
                _row, new Vector2(SlotWidth, SlotHeight),
                PlayerOf() is { } player ? IntentCardBinder.InstanceFor(player, _creature!, card) : null);
            if (view is null)
            {
                // 自绘 UI 已按用户要求删除 ⇒ 拿不到真卡就跳过这一格（日志会从 IntentCardView 里报原因）。
                continue;
            }

            // 凋萎那一项：没有对应的 MoveState（拿不到意图图/数字）⇒ 直接把"凋萎 X"写在卡面数字位上。
            view.Bind(card, _creature!, card.IsVoid ? $"凋萎 {card.HitPointsOf()}" : null);
            view.SetDim(!card.Playable);
            view.SetBaseHighlight(HighlightFor(card, state));
            _views.Add(view);
            // 登记"这个节点是哪张意图卡"（原版卡牌指向的侧通道）。
            if (view.Card is { } node)
            {
                IntentTargetRegistry.Register(node, card, _creature!);
            }

            if (TargetingActive && card.Playable)
            {
                // 指向模式：可行动的那几张可点，点中即结算（悬停给青色高光）。
                var target = card;
                var index = shown;
                view.SetTargeting(() =>
                {
                    // 诊断日志：点的是第几张、哪一张 —— "效果作用到别的牌上"这类问题靠它一眼定位。
                    Const.Logger.Info(
                        $"[{Const.ModId}] {Const.Prefix.Intent} 指向点击：手牌第 {index} 张 {target.Describe()}");
                    IntentTargeting.TrySubmit(_creature!, target);
                });
            }

            shown++;
        }

        var extra = state.Hand.Count - shown;
        if (extra > 0)
        {
            _row.AddChild(BuildLabelSlot($"+{extra}"));
        }

    }

    /// <summary>
    /// 抽 / 弃牌堆指示器：优先用**本体的玩家牌堆按钮外观**（<c>combat_piles_container</c> 里的
    /// <c>%DrawPile</c> / <c>%DiscardPile</c> 子树），拿不到就退回自绘小标。
    /// </summary>
    /// <remarks>
    /// ⚠️ 不能用本体那套的 <c>Initialize(player)</c> —— 那会把计数绑到**玩家**的牌堆上；
    /// 我们只是借它的外观，数量自己写。所以：实例化 → 把节点摘出来 → 找它的第一个 Label 写数量。
    /// </remarks>
    private void AddPileIndicator(bool isDraw, string title, IReadOnlyList<IntentCard> pile)
    {
        var chip = BuildVanillaPileVisual(isDraw, pile.Count, pile.Count(c => c.IsVoid));
        if (chip is null)
        {
            chip = BuildPileChip(title, pile);
        }
        else
        {
            chip.TooltipText = PileTooltip(title, pile);
        }

        _pileRow.AddChild(chip);

        // 用户 2026-10-12：牌堆**点开看预览**（点开/再点关）。
        chip.MouseFilter = MouseFilterEnum.Stop;
        chip.MouseDefaultCursorShape = CursorShape.PointingHand;
        var captured = chip;
        chip.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                // 同一个牌堆再点一次 = 关；点另一个 = 换成那个。
                if (_popupIsDrawPile == isDraw)
                {
                    ClosePilePopup();
                    _popupIsDrawPile = null;
                    return;
                }

                _popupIsDrawPile = isDraw;
                ShowPilePopup(title, pile, _pileRow);
            }
        };

        // 用户 2026-10-12：「卡组详细界面…鼠标离开就消失」——
        // 鼠标离开小标时，若不在面板矩形内就直接关掉（移到面板上不算离开）。
        chip.MouseExited += () =>
        {
            if (_pilePopup is not { } panel || !GodotObject.IsInstanceValid(panel))
            {
                return;
            }

            var mouse = chip.GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            if (!panel.GetGlobalRect().HasPoint(mouse))
            {
                ClosePilePopup();
                _popupIsDrawPile = null;
            }
        };
    }

    private void ClosePilePopup()
    {
        if (_pilePopup is { } existing)
        {
            if (GodotObject.IsInstanceValid(existing))
            {
                existing.QueueFreeSafely();
            }

            _pilePopup = null;
        }
    }

    /// <summary>牌堆预览：在牌堆行下面弹一个小面板，列出这一堆里有什么（同堆再点关、换堆换内容）。</summary>
    private void ShowPilePopup(string title, IReadOnlyList<IntentCard> pile, Control anchor)
    {
        ClosePilePopup();

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.88f),
            BorderColor = new Color(1f, 1f, 1f, 0.35f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        });

        var box = new VBoxContainer();
        panel.AddChild(box);
        box.AddChild(MakePopupLabel($"{title}牌堆（{pile.Count}）", 11, Colors.White));
        foreach (var card in pile.Take(24))
        {
            box.AddChild(MakePopupLabel($"· {card.Describe()}", 10, new Color(1f, 1f, 1f, 0.82f)));
        }

        if (pile.Count == 0)
        {
            box.AddChild(MakePopupLabel("（空）", 10, new Color(1f, 1f, 1f, 0.6f)));
        }

        AddChild(panel);
        panel.Position = anchor.Position + new Vector2(0f, anchor.Size.Y + 2f) + _pileRow.Position;
        panel.ZIndex = 200;
        panel.MouseFilter = MouseFilterEnum.Stop;
        _pilePopup = panel;

        // 鼠标从面板上移开也关（面板要收鼠标事件，否则收不到 Exited）。
        panel.MouseExited += () =>
        {
            var mouse = panel.GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            if (!panel.GetGlobalRect().HasPoint(mouse))
            {
                ClosePilePopup();
                _popupIsDrawPile = null;
            }
        };
    }

    private static Label MakePopupLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>
    /// <b>「拆解」用的选牌面板</b>（INT-4）：把这只怪<b>抽牌堆顶</b>的几张意图画成真卡横排，点一张回调一次并关闭。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 挂在卡带上的理由：卡带本来就在这只怪的意图区上方（坐标同源），怪死了会连带释放，不用另做生命周期。
    /// </para>
    /// <para>
    /// 面板**不自己改牌堆**：点中的那张交给回调（卡 → <c>IntentResolver.Dismantle</c>），那边会再确认一次
    /// "这张还在抽牌堆里"—— 面板开着的这段时间里，回合推进 / 洗牌都可能把它挪走。
    /// </para>
    /// </remarks>
    /// <returns>面板是否真的打开了（拿不到玩家 / 没有可画的卡都返回 false）。</returns>
    internal bool BeginTopPick(IReadOnlyList<IntentCard> cards, Func<IntentCard, string> onPick)
    {
        CloseTopPick();
        if (_creature is null || PlayerOf() is not { } player || cards.Count == 0)
        {
            return false;
        }

        try
        {
            var panel = new PanelContainer
            {
                Name = "AeonglassTopPick",
                MouseFilter = MouseFilterEnum.Stop,
                ZIndex = 210,
                Modulate = new Color(1f, 1f, 1f, 0.97f),
            };

            var box = new VBoxContainer();
            box.AddChild(MakePopupLabel($"拆解 · 抽牌堆顶 {cards.Count} 张（点一张丢进它的弃牌堆）", 11, Colors.White));

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", (int)SlotGap);

            var shown = 0;
            foreach (var card in cards)
            {
                var view = IntentCardView.Create(
                    row, new Vector2(SlotWidth, SlotHeight),
                    IntentCardBinder.InstanceFor(player, _creature, card));
                if (view is null)
                {
                    continue;
                }

                view.Bind(card, _creature, card.IsVoid ? $"凋萎 {card.HitPointsOf()}" : null);
                view.SetDim(!card.Playable);

                var picked = card;
                view.SetTargeting(() =>
                {
                    var text = onPick(picked);
                    Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 拆解点击：{picked.Describe()} ⇒ {text}");
                    CloseTopPick();
                });

                shown++;
            }

            if (shown == 0)
            {
                panel.QueueFreeSafely();
                return false;
            }

            box.AddChild(row);
            panel.AddChild(box);
            AddChild(panel);
            panel.Position = TopPickOffset;
            _picker = panel;

            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 拆解：打开抽牌堆顶选牌面板（{shown} 张，可点选）");
            return true;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 拆解面板打开失败：{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>关掉「拆解」面板（没有就什么都不做）。</summary>
    internal void CloseTopPick()
    {
        if (_picker is { } existing && IsInstanceValid(existing))
        {
            existing.QueueFree();
        }

        _picker = null;
    }

    /// <summary>把本体玩家牌堆按钮的外观子树拿来用（数量自己填）；任何一步失败都返回 null。</summary>
    private static Control? BuildVanillaPileVisual(bool isDraw, int count, int witherCount)
    {
        try
        {
            if (PreloadManager.Cache.GetScene(NCombatPilesContainer.scenePath)?.Instantiate() is not Control container)
            {
                return null;
            }

            var pile = container.GetNodeOrNull<Control>(isDraw ? "%DrawPile" : "%DiscardPile");
            if (pile is null)
            {
                container.QueueFreeSafely();
                return null;
            }

            pile.GetParent()?.RemoveChildSafely(pile);
            container.QueueFreeSafely();
            WriteCountLabel(pile, count.ToString());

            // 套一层定尺寸的槽位（本体按钮比卡带一格大，直接塞进去会把整条撑开）。
            var wrapper = new Control
            {
                CustomMinimumSize = new Vector2(26f, SlotHeight),
                MouseFilter = MouseFilterEnum.Ignore,
                TooltipText = string.Empty,
            };
            wrapper.AddChildSafely(pile);

            var size = pile.Size;
            if (size.X > 1f && size.Y > 1f)
            {
                var scale = Mathf.Min(wrapper.CustomMinimumSize.X / size.X, SlotHeight / size.Y);
                pile.Scale = new Vector2(scale, scale);
                pile.Position = (wrapper.CustomMinimumSize - size * scale) * 0.5f;
            }

            // 数量 Label：**贴在图标右下角**（用户 2026-10-12："数字和图标没在一起" ⇒ 之前锚在槽位角上、离图标太远）。
            // 位置按"图标在槽位里居中"算：图标右下角 = 槽位中心 + 图标显示尺寸的一半，再往内收一点。
            var iconSize = size * (pile.Scale.X > 0f ? pile.Scale.X : 1f);
            var countLabel = new Label
            {
                Name = "AeonglassPileCount",
                Text = count.ToString(),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            countLabel.AddThemeFontSizeOverride("font_size", 12);
            countLabel.AddThemeColorOverride("font_color", Colors.White);
            countLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
            countLabel.AddThemeConstantOverride("outline_size", 4);
            wrapper.AddChild(countLabel);
            // 收进来的那点内边距（10/12）就是微调用的两个数字。
            countLabel.Position = wrapper.CustomMinimumSize * 0.5f + iconSize * 0.5f - new Vector2(-25f, -80f);
            countLabel.ZIndex = 10;

            // 用户 2026-10-12：「加一个显示，抽弃」—— 这一堆里**凋萎有几张**，抽牌堆与弃牌堆都显示。
            // 0 张时不画（不占地方）。位置：贴在图标**上边**（数字在下面，两者不打架）；
            // 末行那两个数字（-6 / -14）就是左右 / 上下的微调旋钮。
            if (witherCount > 0)
            {
                var wither = new Label
                {
                    Name = "AeonglassPileWither",
                    Text = $"凋萎 {witherCount}",
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                wither.AddThemeFontSizeOverride("font_size", 9);
                wither.AddThemeColorOverride("font_color", new Color("C9A0FF"));
                wither.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
                wither.AddThemeConstantOverride("outline_size", 4);
                wrapper.AddChild(wither);
                wither.Position = wrapper.CustomMinimumSize * 0.5f - iconSize * 0.5f + new Vector2(-6f, -14f);
                wither.ZIndex = 10;
            }

            return wrapper;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 借用玩家牌堆外观失败（退回自绘小标）：{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 把数量写进本体牌堆按钮的**计数** Label。
    /// </summary>
    /// <remarks>
    /// ⚠️ 不能"找到第一个 Label 就写"（用户 2026-10-12 实测：显示的还是 0）——本体那个按钮里还有别的
    /// Label（快捷键之类）。这里只认"当前文本是纯数字"的那些：计数 Label 没 Initialize 时就是 "0"，
    /// 而被我们漏掉的计数 Label 也正是它 ⇒ 直接按这个特征写。
    /// </remarks>
    private static void WriteCountLabel(Node node, string text)
    {
        if (node is Label label)
        {
            var current = label.Text?.Trim() ?? string.Empty;
            if (current.Length > 0 && current.All(char.IsDigit))
            {
                label.Text = text;
            }
        }

        foreach (var child in node.GetChildren())
        {
            WriteCountLabel(child, text);
        }
    }

    /// <summary>牌堆小标的悬浮说明（两版共用）。</summary>
    private static string PileTooltip(string title, IReadOnlyList<IntentCard> pile) =>
        pile.Count == 0
            ? $"{title}牌堆：空"
            : $"{title}牌堆（{pile.Count}）："
              + string.Join(", ", pile.Take(12).Select(c => c.IsVoid ? "虚无" : c.MoveId))
              + (pile.Count > 12 ? " …" : string.Empty);

    /// <summary>
    /// "抽 / 弃"牌堆也做成**一张卡**（用户 2026-10-12）：卡图 = 堆顶那张意图的图，卡面数字 = 这一堆的张数；
    /// 空堆就只有 0、并且灰掉。位置本身就把两堆区分开了（左抽 / 右弃）。
    /// </summary>
    /// <remarks>节点由 <see cref="IntentCardView.Create" /> 直接挂进 <c>_row</c>，所以这里不返回值、调用方也不要再 AddChild。</remarks>
    private void BuildPileCard(IReadOnlyList<IntentCard> pile, Creature owner)
    {
        var view = IntentCardView.Create(_row, new Vector2(SlotWidth, SlotHeight));
        if (view is null)
        {
            return;
        }

        var top = pile.Count == 0 ? null : pile[0];
        view.Bind(top, owner, pile.Count.ToString());
        view.SetDim(pile.Count == 0);
    }

    /// <summary>一个牌堆小标（"抽 3" / "弃 2"），悬浮看内容。</summary>
    private static Control BuildPileChip(string title, IReadOnlyList<IntentCard> pile)
    {
        var chip = new PanelContainer
        {
            CustomMinimumSize = new Vector2(22f, SlotHeight),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.08f, 0.80f),
            BorderColor = new Color(1f, 1f, 1f, 0.28f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        });

        var label = new Label
        {
            Text = $"{title}{pile.Count}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 8);
        label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.8f));
        chip.AddChild(label);

        chip.TooltipText = pile.Count == 0
            ? $"{title}牌堆：空"
            : $"{title}牌堆（{pile.Count}）："
              + string.Join(", ", pile.Take(12).Select(c => c.IsVoid ? "虚无" : c.MoveId))
              + (pile.Count > 12 ? " …" : string.Empty);
        return chip;
    }

    /// <summary>一张意图卡：空槽卡框 + 图标 + 角标（条件✗ / 强制 / 消耗 / 被吃）。</summary>
    private static Control BuildLabelSlot(string text)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.75f));
        return label;
    }

}
