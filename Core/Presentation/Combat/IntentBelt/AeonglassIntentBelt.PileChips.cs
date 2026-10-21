using Godot;

using System.Diagnostics;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Presentation.Support;
using STS2_Aeonglass.Core.Systems.Intent;
using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
/// <summary>卡带下方的「抽 / 弃牌堆」小卡与悬停 popup（<c>AeonglassIntentBelt</c> 的 partial 部分）。</summary>
/// <remarks>
/// ① 职责：把两堆画成原版样式的小卡（堆顶图 + 数量），悬停展开内容。
/// ② 调用时机：卡带刷新时按"牌堆签名"重建（内容没变就不重建）。
/// ③ 依赖：原版牌堆按钮外观 + <c>UiFactory</c>。
/// ④ ⚠️ popup 在鼠标离开 / 重建时关闭；<c>WriteCountLabel</c> 只替换纯数字文本，别把它当通用文本重写。
/// </remarks>

public partial class AeonglassIntentBelt : Control
{

    /// <summary>牌堆小标的几何只打一行（整个进程一次），调标签位置时对着它改。</summary>
    private static bool LoggedPileChipGeometry;

    /// <summary>重建次数（只服务"性能探针"那行日志；默认关，见设置 json 的 <c>DebugIntentBeltLog</c>）。</summary>
    private int _rebuildCount;

    /// <summary>
    /// 一张抽/弃小卡：**建一次常驻**（2026-10-15 池化），之后刷新只改内容。
    /// </summary>
    /// <remarks>
    /// ⚠️ `Cell`（挂进 `_pileRow` 的那个 wrapper）建的时候还没进树、量不到真实变换 ⇒
    /// 缩放/居中/摆标签都推迟到挂进树、排版出尺寸之后（见 <see cref="ResolvePendingChips" />）。
    /// </remarks>
    private sealed class PileChipSlot
    {
        /// <summary>true = 抽牌堆那一张。</summary>
        public bool IsDraw;

        /// <summary>true = 借的本体牌堆按钮外观；false = 自绘兜底（拿不到本体场景时）。</summary>
        public bool Vanilla;

        /// <summary>挂进 <paramref name="_pileRow" /> 的格子（鼠标事件也挂它）。</summary>
        public Control Cell = null!;

        /// <summary>图标真身（本体按钮 / 自绘面板）。</summary>
        public Control Icon = null!;

        /// <summary>我们自己写的数量标签（只有借用本体外观时才有）。</summary>
        public Label? Count;

        /// <summary>凋萎数量标签（两版都有，0 张时不可见）。</summary>
        public Label? Wither;

        /// <summary>自绘兜底版的"抽 3"文本。</summary>
        public Label? FallbackText;
    }

    /// <summary>待排版的小卡（建好但还没量到尺寸 ⇒ 推迟做缩放/居中/摆标签）。</summary>
    private readonly List<PileChipSlot> _pendingChips = [];

    /// <summary>抽 / 弃两张**常驻**小卡（懒建；之后只改内容，不再重建）。</summary>
    private PileChipSlot? _drawChip;
    private PileChipSlot? _discardChip;

    /// <summary>
    /// **抽/弃小卡的图标相对格子的额外偏移**（格子本地单位）—— 图标位置不对就改这一行。
    /// </summary>
    /// <remarks>
    /// <para>默认 <c>(0, 0)</c>：图标由 <c>UiGeometry.FitAndCenterIn</c> **实测**居中，通常不用动。</para>
    /// <para>X 正 = 右、Y 正 = 下；单位是格子本地单位（屏幕位移 = 这个值 × 相机缩放）。
    /// 「数量」「凋萎 N」两个标签是照图标真身矩形摆的，会跟着一起走，不用另调。</para>
    /// </remarks>
    private static readonly Vector2 PileChipIconNudge = new(0f, 0f);

    /// <summary>
    /// 抽 / 弃牌堆指示器：优先用**本体的玩家牌堆按钮外观**（<c>combat_piles_container</c> 里的
    /// <c>%DrawPile</c> / <c>%DiscardPile</c> 子树），拿不到就退回自绘小标。
    /// </summary>
    /// <remarks>
    /// ⚠️ 不能用本体那套的 <c>Initialize(player)</c> —— 那会把计数绑到**玩家**的牌堆上；
    /// 我们只是借它的外观，数量自己写。所以：实例化 → 把节点摘出来 → 找它的第一个 Label 写数量。
    /// </remarks>
    /// <summary>
    /// 刷出抽 / 弃两张小卡（**池化**：第一次建节点，之后每次只改内容）。
    /// </summary>
    /// <remarks>
    /// 旧版每次 `UpdateRow` 都 `ClearPileRow()` + 重新 `Instantiate` 本体牌堆场景 + 重建标签，
    /// 每次手牌变化都付一遍场景实例化与节点释放的成本；现在两张卡常驻、只更新文本/显隐/tooltip/预览。
    /// </remarks>
    private void RefreshPileChips(EnemyIntentState state)
    {
        RefreshPileChip(isDraw: true, "抽", state.Deck);
        RefreshPileChip(isDraw: false, "弃", state.Discard);
    }

    private void RefreshPileChip(bool isDraw, string title, IReadOnlyList<IntentCard> pile)
    {
        var slot = isDraw ? _drawChip : _discardChip;
        if (slot is null || !GodotObject.IsInstanceValid(slot.Cell))
        {
            slot = BuildPileChipSlot(isDraw, title, pile);
            if (slot is null)
            {
                return;
            }

            if (isDraw)
            {
                _drawChip = slot;
            }
            else
            {
                _discardChip = slot;
            }

            _pileRow.AddChild(slot.Cell);
            WirePileChip(slot, title);
            // 顺序固定：抽在前、弃在后（MoveChild 到同一位置是空操作）。
            _pileRow.MoveChild(slot.Cell, Math.Clamp(isDraw ? 0 : 1, 0, Math.Max(0, _pileRow.GetChildCount() - 1)));
        }

        UpdatePileChip(slot, title, pile);
    }

    /// <summary>点开预览（点开/再点关）+ 鼠标离开就消失 —— 只接一次线，点击时**取当前数据**（池化后不能再用建卡时的快照）。</summary>
    private void WirePileChip(PileChipSlot slot, string title)
    {
        var cell = slot.Cell;
        var isDraw = slot.IsDraw;
        cell.MouseFilter = MouseFilterEnum.Stop;
        cell.MouseDefaultCursorShape = CursorShape.PointingHand;

        cell.GuiInput += @event =>
        {
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                return;
            }

            // 同一个牌堆再点一次 = 关；点另一个 = 换成那个。
            if (_popupIsDrawPile == isDraw)
            {
                ClosePilePopup();
                _popupIsDrawPile = null;
                return;
            }

            if (CurrentPile(isDraw) is { } live)
            {
                _popupIsDrawPile = isDraw;
                ShowPilePopup(title, live, _pileRow);
            }
        };

        cell.MouseExited += () =>
        {
            if (_pilePopup is not { } panel || !GodotObject.IsInstanceValid(panel))
            {
                return;
            }

            var mouse = cell.GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            if (!panel.GetGlobalRect().HasPoint(mouse))
            {
                ClosePilePopup();
                _popupIsDrawPile = null;
            }
        };
    }

    /// <summary>这一侧牌堆的**当前**内容（拿不到就给空表）。</summary>
    private IReadOnlyList<IntentCard> CurrentPile(bool isDraw)
    {
        if (_creature is not null && IntentDeck.TryGet(_creature, out var state))
        {
            return isDraw ? state.Deck : state.Discard;
        }

        return [];
    }

    /// <summary>只改内容：数量 / 凋萎 / tooltip；预览面板开着就顺手用新内容重建。</summary>
    private void UpdatePileChip(PileChipSlot slot, string title, IReadOnlyList<IntentCard> pile)
    {
        var count = pile.Count.ToString();
        var witherCount = pile.Count(c => c.IsVoid);

        slot.Cell.TooltipText = PileTooltip(title, pile);

        if (slot.Vanilla)
        {
            WriteCountLabel(slot.Icon, count);
            if (slot.Count is { } label)
            {
                label.Text = count;
            }
        }
        else if (slot.FallbackText is { } fallback)
        {
            fallback.Text = $"{title}{count}";
        }

        if (slot.Wither is { } wither)
        {
            wither.Text = $"凋萎 {witherCount}";
            wither.Visible = witherCount > 0;
        }

        if (_popupIsDrawPile == slot.IsDraw)
        {
            ShowPilePopup(title, pile, _pileRow);
        }
    }


    /// <summary>
    /// 造一张抽/弃小卡：优先借本体玩家牌堆按钮的外观（数量自己填），拿不到就退回自绘小标。
    /// </summary>
    /// <remarks>只在**第一次**需要这一侧时调（之后走 <see cref="UpdatePileChip" /> 只改内容）。</remarks>
    private PileChipSlot? BuildPileChipSlot(bool isDraw, string title, IReadOnlyList<IntentCard> pile)
    {
        var vanilla = BuildVanillaPileSlot(isDraw, pile.Count, pile.Count(c => c.IsVoid));
        if (vanilla is not null)
        {
            return vanilla;
        }

        return BuildFallbackPileSlot(isDraw, title, pile);
    }

    /// <summary>把本体玩家牌堆按钮的外观子树拿来用（数量自己填）；任何一步失败都返回 null。</summary>
    private PileChipSlot? BuildVanillaPileSlot(bool isDraw, int count, int witherCount)
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

            // 数量 Label：**贴在图标右下角**（用户 2026-10-12："数字和图标没在一起" ⇒ 之前锚在槽位角上、离图标太远）。
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
            countLabel.ZIndex = 10;

            // 用户 2026-10-12：「加一个显示，抽弃」—— 这一堆里**凋萎有几张**，抽牌堆与弃牌堆都显示。
            // 位置：贴在图标**上边**（数字在下面，两者不打架）；末行那两个数字（-6 / -14）就是微调旋钮。
            // ⚠️ 池化后**永远建**（0 张时只 `Visible = false`）—— 否则第一次没凋萎、之后就永远显示不出来。
            var witherLabel = new Label
            {
                Name = "AeonglassPileWither",
                Text = $"凋萎 {witherCount}",
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = witherCount > 0,
            };
            witherLabel.AddThemeFontSizeOverride("font_size", 9);
            witherLabel.AddThemeColorOverride("font_color", new Color("C9A0FF"));
            witherLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
            witherLabel.AddThemeConstantOverride("outline_size", 4);
            wrapper.AddChild(witherLabel);
            witherLabel.ZIndex = 10;

            // ⚠️ 缩放/居中/摆标签**不能在这里做**：此刻 wrapper 还没挂进 `_pileRow`（调用方下一行才 AddChild），
            //    `GetGlobalTransformWithCanvas()` 量到的是无效变换 ⇒ 图标与两个标签全偏（实测踩到）。
            //    所以只登记，等挂进树、格子排版出尺寸之后由 `ResolvePendingChips` 收尾。
            var slot = new PileChipSlot
            {
                IsDraw = isDraw,
                Vanilla = true,
                Cell = wrapper,
                Icon = pile,
                Count = countLabel,
                Wither = witherLabel,
            };

            _pendingChips.Add(slot);
            return slot;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 借用玩家牌堆外观失败（退回自绘小标）：{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>自绘兜底小标（拿不到本体外观时）：一个带边框的小面板 + "抽 3" 文本。</summary>
    private static PileChipSlot? BuildFallbackPileSlot(bool isDraw, string title, IReadOnlyList<IntentCard> pile)
    {
        var chip = BuildPileChip(title, pile);
        return chip is null
            ? null
            : new PileChipSlot
            {
                IsDraw = isDraw,
                Vanilla = false,
                Cell = chip,
                Icon = chip,
                FallbackText = chip.GetChildren().OfType<Label>().FirstOrDefault(),
            };
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

        chip.TooltipText = PileTooltip(title, pile);
        return chip;
    }

    /// <summary>卡带尾部的一行说明文字（如 <c>+2</c>：这一版多出来的张数）。</summary>
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

    /// <summary>
    /// 增量更新整条卡带（2026-10-15 改造）：**按 identity 复用真卡视图**，节点只在"从无到有"时创建。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 旧版是"整条 QueueFree + 逐张 Create"（每次状态变化都付一遍 NCard 绑定 + 隐藏 chrome + 字号换算的成本）；
    /// 现在：同一张意图 ⇒ 只刷卡面数字；换了牌 ⇒ 借一个空槽视图 <c>Rebind</c>（重设模型）；只有槽位不够时才新建。
    /// 手牌只是**换顺序**时，连 Rebind 都不需要，只把槽位节点的兄弟序排一遍。
    /// </para>
    /// <para>
    /// 调用前已经由 <c>FingerprintChanged</c> 判定过"确实需要刷"，所以这里不做二次判定。
    /// </para>
    /// </remarks>
    private void UpdateRow(EnemyIntentState? state)
    {
        if (state is null)
        {
            _animationsRefreshed = false;
            // 发牌特效放着放着"手牌空了"：立刻收尾（卡反正要消失，别让它停在半透明）。
            if (_dealing)
            {
                FinishDeal();
            }

            foreach (var view in _views)
            {
                view.SetBaseHighlight(IntentHighlight.None);
                view.Park();
            }

            _views.Clear();
            UnregisterOwnNodes();
            ClosePilePopup();
            Visible = false;
            _rowWidthDirty = true;

            if (_lastShown != false)
            {
                _lastShown = false;
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 卡带隐藏（手牌空）：monster={_creature?.Monster?.Id.Entry ?? "-"}");
            }

            return;
        }

        _lastShown = true;
        Visible = true;
        UnregisterOwnNodes();
        var openKind = _popupIsDrawPile;
        ClosePilePopup();

        // 性能探针：从"确定要重建"这一刻起计时（上面那几处早退不算），计数器 + Stopwatch 只在开关打开时才读。
        var watch = Stopwatch.StartNew();
        _rebuildCount++;

        if (AeonglassSettingStore.Current.DebugIntentBeltLog)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡带更新：手牌 {state.Hand.Count} 张 "
                + $"[{string.Join(",", state.Hand.Select(c => c.MoveId))}]｜"
                + $"pending={(state.Pending?.MoveId ?? "-")}｜判定={state.Decision}");
        }

        var player = PlayerOf();
        var limit = Math.Min(state.Hand.Count, MaxSlots);
        var slots = new List<AeonglassIntentCardView>(limit);
        var spare = new List<AeonglassIntentCardView>(_views);

        for (var i = 0; i < limit; i++)
        {
            var card = state.Hand[i];
            // 凋萎那一项：没有对应的 MoveState（拿不到意图图/数字）⇒ 直接把"凋萎 X"写在卡面数字位上。
            var numberOverride = card.IsVoid ? $"凋萎 {card.HitPointsOf()}" : null;

            // ① 同一张意图的视图：直接复用（只刷卡面）。
            var view = spare.FirstOrDefault(v => !slots.Contains(v) && ReferenceEquals(v.Identity, card));
            if (view is not null && view.IsAlive)
            {
                view.RefreshFace(card, _creature!, numberOverride);
            }
            else
            {
                // ② 换一张：借一个空槽视图换绑；没有可借的才新建（顺序无关，所以"只是换位"不会触发 Rebind）。
                var reusable = spare.FirstOrDefault(v => !slots.Contains(v) && v.IsAlive);
                var model = player is not null ? IntentCardBinder.InstanceFor(player, _creature!, card) : null;
                if (reusable is not null)
                {
                    reusable.Rebind(card, _creature!, model, numberOverride);
                    view = reusable;
                }
                else
                {
                    view = AeonglassIntentCardView.Create(_row, new Vector2(SlotWidth, SlotHeight), model);
                    if (view is null)
                    {
                        // 拿不到真卡就跳过这一格（日志由 AeonglassIntentCardView 自己报）。
                        continue;
                    }

                    view.Bind(card, _creature!, numberOverride);
                }
            }

            slots.Add(view);
            view.SetSlot(i, limit);
            view.SetDim(!card.Playable);
            view.SetBaseHighlight(HighlightFor(card, state));

            // 登记"这个节点是哪张意图卡"（原版卡牌指向的侧通道）。
            if (view.Card is { } node)
            {
                IntentTargetRegistry.Register(node, card, _creature!);
                _registeredNodes.Add(node);
            }

            // 指向不再走"卡带点击"：现在用**本体卡牌选目标那套**（见 Core/Patches/IntentTargetingPatches.cs），
            // 所以这里不再挂点击回调（原来那套 intent 点名链路实测 0 调用，2026-10-15 已删）。
        }

        // 归位：按手牌顺序把槽位排一遍（MoveChild 到同一位置是空操作，代价可忽略）。
        for (var i = 0; i < slots.Count; i++)
        {
            slots[i].AttachTo(_row);
            _row.MoveChild(slots[i].Wrapper, i);
        }

        // 这次没用上的视图：收掉悬停/高光后从行里撤下来（留着下次复用，不 QueueFree）。
        foreach (var old in _views)
        {
            if (slots.Contains(old))
            {
                continue;
            }

            old.SetBaseHighlight(IntentHighlight.None);
            old.Park();
        }

        _views.Clear();
        _views.AddRange(slots);

        // 发牌特效进行中：把"当前扫描位置对应的显现进度"重新摊到复用/新建的视图上（换牌不闪回半透明）。
        ReapplyDealReveal();

        // 溢出提示（+N）：复用同一个节点，永远排在卡面之后。
        var extra = state.Hand.Count - slots.Count;
        if (extra > 0)
        {
            if (_overflowLabel is null || !GodotObject.IsInstanceValid(_overflowLabel))
            {
                _overflowLabel = BuildLabelSlot($"+{extra}");
            }

            ((Label)_overflowLabel).Text = $"+{extra}";
            _row.AddChildSafely(_overflowLabel);
            _row.MoveChild(_overflowLabel, -1);
        }
        else if (_overflowLabel is { } label && GodotObject.IsInstanceValid(label))
        {
            label.GetParent()?.RemoveChildSafely(label);
        }

        // 牌堆行（第一张卡正下方）：抽 / 弃 两格，用**玩家牌堆那种**样式。
        // ⚠️ 池化（2026-10-15）：两张卡建一次常驻，之后只改内容 —— 不再每次更新都 Instantiate + QueueFree。
        RefreshPileChips(state);

        // 一次性诊断（每场战斗每只怪一行）：卡带出图时把"用了哪张锚点、行宽多少、落在哪"打出来，
        // 位置不对时对着这一行看数值即可（不是每帧，代价可忽略）。
        if (!_loggedReady && AeonglassSettingStore.DebugVerboseLogs)
        {
            _loggedReady = true;
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡带就绪：monster={state.MonsterId} 卡面 {slots.Count} 张"
                + $"｜锚点 local pos={_anchor?.Position} size={_anchor?.Size}"
                + $"｜行宽={_rowWidth:0.#}｜卡带 pos={Position}｜父={GetParent()?.Name}"
                + $"｜相机缩放={GetGlobalTransformWithCanvas().Scale.X:0.###}（本地单位 × 它 = 屏幕像素）");
        }

        // 预览面板如果开着 ⇒ 用**当前**内容重建一次。
        // 这既修"自动修改后没刷新"，也修"关掉再点还是旧内容"（每次刷新时面板都跟着重算）。
        if (openKind is { } wasDraw)
        {
            ShowPilePopup(wasDraw ? "抽" : "弃", wasDraw ? state.Deck : state.Discard, _pileRow);
        }

        _rowWidthDirty = true;

        // 发牌特效：卡带第一次出图（= 进入战斗后的首次有牌）时起一次，之后不再放。
        BeginDealIfNeeded(state);
        _animationsRefreshed = true;

        // 性能探针（默认关）：重建次数 + 耗时 —— 这条链路里唯一贵的操作就是重建（视图/卡面/登记），
        // 以后要收窄刷新粒度或池化前，先看这两行数据。
        if (AeonglassSettingStore.Current.DebugIntentBeltLog)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡带重建 #{_rebuildCount}｜槽位 {slots.Count}/{state.Hand.Count}"
                + $"｜耗时 {watch.Elapsed.TotalMilliseconds:0.##} ms");
        }
    }

    /// <summary>
    /// 撤掉本卡带之前登记过的节点（**不动**别人怪的登记）。
    /// </summary>
    /// <remarks>
    /// 登记表是全局的；原来的做法是每条卡带刷新时整表 <c>Clear()</c>，多点战斗里会把别的怪的卡
    /// 一起清掉（指向失效）。现在只撤自己这几个。
    /// </remarks>
    private void UnregisterOwnNodes()
    {
        foreach (var node in _registeredNodes)
        {
            IntentTargetRegistry.Unregister(node);
        }

        _registeredNodes.Clear();
    }

    /// <summary>
    /// 只重算可见卡面的数字（不动结构、不动高光）。
    /// </summary>
    /// <remarks>
    /// 给"力量 / 易伤这类加成改了数字，但意图状态没变"的情况兜底：内部走 <c>RefreshFace</c>，
    /// 文本没变就什么都不做，所以稳态下几乎零成本。
    /// </remarks>
    private void RefreshFacesOnly(EnemyIntentState state)
    {
        if (_creature is null)
        {
            return;
        }

        var limit = Math.Min(state.Hand.Count, _views.Count);
        for (var i = 0; i < limit; i++)
        {
            var card = state.Hand[i];
            var view = _views[i];
            if (!ReferenceEquals(view.Identity, card) || !view.IsAlive)
            {
                continue;
            }

            view.RefreshFace(card, _creature, card.IsVoid ? $"凋萎 {card.HitPointsOf()}" : null);
        }
    }
}
