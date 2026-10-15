using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Presentation.Support;
using STS2_Aeonglass.Core.Systems.Intent;


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

    /// <summary>
    /// "抽 / 弃"牌堆也做成**一张卡**（用户 2026-10-12）：卡图 = 堆顶那张意图的图，卡面数字 = 这一堆的张数；
    /// 空堆就只有 0、并且灰掉。位置本身就把两堆区分开了（左抽 / 右弃）。
    /// </summary>
    /// <remarks>节点由 <see cref="AeonglassIntentCardView.Create" /> 直接挂进 <c>_row</c>，所以这里不返回值、调用方也不要再 AddChild。</remarks>
    private void BuildPileCard(IReadOnlyList<IntentCard> pile, Creature owner)
    {
        var view = AeonglassIntentCardView.Create(_row, new Vector2(SlotWidth, SlotHeight));
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
            var view = AeonglassIntentCardView.Create(
                _row, new Vector2(SlotWidth, SlotHeight),
                PlayerOf() is { } player ? IntentCardBinder.InstanceFor(player, _creature!, card) : null);
            if (view is null)
            {
                // 自绘 UI 已按用户要求删除 ⇒ 拿不到真卡就跳过这一格（日志会从 AeonglassIntentCardView 里报原因）。
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
}
