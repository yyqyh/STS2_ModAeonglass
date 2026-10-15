using Godot;

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
        box.AddChild(UiFactory.MakeLabel($"{title}牌堆（{pile.Count}）", 11, Colors.White));
        foreach (var card in pile.Take(24))
        {
            box.AddChild(UiFactory.MakeLabel($"· {card.Describe()}", 10, new Color(1f, 1f, 1f, 0.82f)));
        }

        if (pile.Count == 0)
        {
            box.AddChild(UiFactory.MakeLabel("（空）", 10, new Color(1f, 1f, 1f, 0.6f)));
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
            foreach (var view in _views)
            {
                view.SetBaseHighlight(IntentHighlight.None);
                view.Park();
            }

            _views.Clear();
            IntentTargetRegistry.Clear();
            ClearPileRow();
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
        IntentTargetRegistry.Clear();
        var openKind = _popupIsDrawPile;
        ClearPileRow();

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
            }

            if (TargetingActive && card.Playable)
            {
                // 指向模式：可行动的那几张可点，点中即结算。
                var target = card;
                var index = i;
                view.SetTargeting(() =>
                {
                    // 诊断日志：点的是第几张、哪一张 —— "效果作用到别的牌上"这类问题靠它一眼定位。
                    Const.Logger.Info(
                        $"[{Const.ModId}] {Const.Prefix.Intent} 指向点击：手牌第 {index} 张 {target.Describe()}");
                    IntentTargeting.TrySubmit(_creature!, target);
                });
            }
            else
            {
                // 不在指向模式（或这张不可行动）：撤掉点击回调，免得"随手点一下"也提交意图操作。
                view.ClearTargeting();
            }
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

        // 牌堆行（第一张卡正下方）：抽 / 弃 两格，用**玩家牌堆那种**样式（只有两个小节点，照旧重建）。
        AddPileIndicator(isDraw: true, "抽", state.Deck);
        AddPileIndicator(isDraw: false, "弃", state.Discard);

        // 一次性诊断（每场战斗每只怪一行）：卡带出图时把"用了哪张锚点、行宽多少、落在哪"打出来，
        // 位置不对时对着这一行看数值即可（不是每帧，代价可忽略）。
        if (!_loggedReady)
        {
            _loggedReady = true;
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡带就绪：monster={state.MonsterId} 卡面 {slots.Count} 张"
                + $"｜锚点 local pos={_anchor?.Position} size={_anchor?.Size}"
                + $"｜行宽={_rowWidth:0.#}｜卡带 pos={Position}｜父={GetParent()?.Name}");
        }

        // 预览面板如果开着 ⇒ 用**当前**内容重建一次。
        // 这既修"自动修改后没刷新"，也修"关掉再点还是旧内容"（每次刷新时面板都跟着重算）。
        if (openKind is { } wasDraw)
        {
            ShowPilePopup(wasDraw ? "抽" : "弃", wasDraw ? state.Deck : state.Discard, _pileRow);
        }

        _rowWidthDirty = true;
    }

    /// <summary>清掉牌堆行与它的 popup（增量更新前先收干净）。</summary>
    private void ClearPileRow()
    {
        foreach (var child in _pileRow.GetChildren())
        {
            child.QueueFree();
        }

        _pilePopup?.QueueFree();
        _pilePopup = null;
        _popupIsDrawPile = null;
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
