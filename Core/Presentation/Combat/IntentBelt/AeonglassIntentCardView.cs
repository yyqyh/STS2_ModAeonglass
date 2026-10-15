using Godot;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
/// <summary>一张「意图卡」的视图：<b>真卡外壳</b>（本体 <c>NCard</c>）+ 我们填进去的内容。</summary>
/// <remarks>
/// ① 职责：卡图 = 该意图自己的图；卡面只留动态数字；名字/描述/费用/类型全隐藏；按 <c>IntentHighlight</c> 上高光。
/// ② 调用时机：卡带 / 顶上选牌面板构建时由 <c>Create</c> 建。
/// ③ 依赖：意图的 <c>IntentCard</c> 模型 + 意图图 + <c>IntentHighlight</c>。
/// ④ ⚠️ 能"一个注册类撑起每张意图一张真卡"的前提：本体 <c>Title / PortraitPath</c> 是 virtual —— 本体若改可见性这里会塌。
/// </remarks>

internal sealed class AeonglassIntentCardView
{
    /// <summary>要隐藏的节点（唯一名，与本体 <c>NCard._Ready</c> 里取的一致）。</summary>
    private static readonly string[] HiddenChrome =
    [
        "%TitleLabel",
        "%TitleBanner",
        "%TypeLabel",
        "%TypePlaque",
        "%EnergyLabel",
        "%EnergyIcon",
        "%StarLabel",
        "%StarIcon",
        "%Enchantment",
        "%Lock",
        "%PortraitBorder",
    ];

    private readonly NCard _card;
    private readonly Control _wrapper;
    private readonly RichTextLabel? _number;
    private Vector2 _slotSize = Vector2.One;
    private Vector2 _baseScale = Vector2.One;
    private Vector2 _baseSize = Vector2.One;

    /// <summary>高光那张（怪物将要打出的 / 我们选中的）比别人**大 20%**（用户 2026-10-12）。</summary>
    private const float EmphasisScale = 1.2f;

    /// <summary>
    /// 卡面上那个数字**希望显示成多大**（屏幕像素；用户 2026-10-12：原来太小）。
    /// </summary>
    /// <remarks>
    /// ⚠️ 字号是**卡的局部坐标**：整张卡被缩到卡带一格（约 ×0.18），直接写 30 号字落到屏幕上只有 5px。
    /// 所以真正写进去的字号 = 这个目标像素 ÷ 缩放比（见 <see cref="ResizeTo" />）。
    /// </remarks>
    private const float TargetNumberPixels = 13f;

    private IntentHighlight _highlight = IntentHighlight.None;
    private IntentHighlight _baseHighlight = IntentHighlight.None;
    private bool _loggedMissingNumber;
    /// <summary>字号那行日志**整个进程只打一次**（原来是每个视图打一次 ⇒ 刷屏）。</summary>
    private static bool _loggedNumberSize;

    /// <summary>我们选中时的高光色（沙漏青）。</summary>
    private static readonly Color SelectedColor = new("7FE7DD");

    private AeonglassIntentCardView(NCard card, Control wrapper, RichTextLabel? number)
    {
        _card = card;
        _wrapper = wrapper;
        _number = number;
    }

    /// <summary>占位用的槽位节点（容器按它的最小尺寸排版；真卡缩放在里面，不影响布局）。</summary>
    internal Control Wrapper => _wrapper;

    /// <summary>卡节点（布局 / 点击都直接用它）。</summary>
    internal NCard Card => _card;

    internal bool IsAlive => GodotObject.IsInstanceValid(_card) && _card.IsInsideTree();

    /// <summary>造一张意图卡视图并挂到 <paramref name="parent" /> 下，按 <paramref name="slotSize" /> 缩放。</summary>
    internal static AeonglassIntentCardView? Create(Node parent, Vector2 slotSize, CardModel? model = null)
    {
        try
        {
            // 模型驱动（计划 1/2 口）：传进来的实例自带该意图的卡图；没传就退回外壳的规范实例。
            if (NCard.Create(model ?? ModelDb.Card<AeonglassIntentCard>()) is not { } card)
            {
                return null;
            }

            // 槽位容器按"上一版的卡牌大小"排版，真卡缩放后**居中**放在里面：
            // 直接缩放 NCard 不会改变它的 size，会被 HBox 按原始尺寸排版（整条卡带会被撑开）。
            var wrapper = new Control
            {
                Name = "IntentCardSlot",
                CustomMinimumSize = slotSize,
                // ⚠️ 必须是 Stop：要收到 MouseEntered/Exited 才能把"悬停在意图卡上"上报给本体瞄准管理器
                //（本体那边是 NCreature 自己上报的，NCard 不会 ⇒ 没人替我们调就不会被当成目标）。
                MouseFilter = Control.MouseFilterEnum.Stop,
            };

            parent.AddChildSafely(wrapper);
            wrapper.AddChildSafely(card);

            foreach (var path in HiddenChrome)
            {
                if (card.GetNodeOrNull<CanvasItem>(path) is { } node)
                {
                    node.Visible = false;
                }
            }

            var number = card.GetNodeOrNull<RichTextLabel>("%DescriptionLabel");
            if (number is not null)
            {
                // 用户 2026-10-12：数字**居中**（横向；纵向靠它原本在卡面上的位置）。
                number.HorizontalAlignment = HorizontalAlignment.Center;
            }

            var view = new AeonglassIntentCardView(card, wrapper, number);
            view.ConnectEngineHover();
            view.ResizeTo(slotSize);
            view.SetInteractable(false);
            return view;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 意图卡视图创建失败：{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>缩放到卡带槽位的尺寸（= 上一版的卡牌大小，等比、取小的那一维）。</summary>
    private void ResizeTo(Vector2 slotSize)
    {
        var current = _card.GetCurrentSize();
        if (current.X <= 1f || current.Y <= 1f || slotSize.X <= 1f || slotSize.Y <= 1f)
        {
            return;
        }

        var scale = Mathf.Min(slotSize.X / current.X, slotSize.Y / current.Y);
        _slotSize = slotSize;
        _baseSize = current * scale;
        _baseScale = new Vector2(scale, scale);
        ApplyScale(1f);

        // 字号按缩放反算：目标屏幕像素 ÷ 缩放 ⇒ 看起来就是 TargetNumberPixels 那么大。
        if (_number is not null && scale > 0.0001f)
        {
            var fontSize = Mathf.Max(12, Mathf.RoundToInt(TargetNumberPixels / scale));

            // ⚠️ RichTextLabel 不吃 `font_size` 这个主题项（那是 Label 的），它用的是 `normal_font_size`
            //（还有 bold/mono 两个）；两个都设上最稳，另外日志把这行打出来，方便一眼确认到底有没有生效。
            _number.AddThemeFontSizeOverride("normal_font_size", fontSize);
            _number.AddThemeFontSizeOverride("bold_font_size", fontSize);
            _number.AddThemeFontSizeOverride("font_size", fontSize);

            if (!_loggedNumberSize)
            {
                _loggedNumberSize = true;
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 意图卡数字：label={_number.GetType().Name} "
                    + $"缩放={scale:0.###} 字号={fontSize}（目标屏幕 {TargetNumberPixels:0}px）");
            }
        }
    }

    /// <summary>按倍率摆放：缩放 + 重新居中（放大时不越出槽位中心）。</summary>
    private void ApplyScale(float factor)
    {
        if (!IsAlive)
        {
            return;
        }

        _card.Scale = _baseScale * factor;
        _card.Position = (_slotSize - _baseSize * factor) * 0.5f;
    }

    /// <summary>
    /// 绑定一张意图：卡图 = 意图的图，文字 = 只剩数字。
    /// </summary>
    /// <param name="card">要绑定的意图；<c>null</c> = 只显示 <paramref name="numberOverride" />（抽/弃牌堆显示堆顶那张时用）。</param>
    /// <param name="owner">这张意图属于哪只怪（取图 / 取数字要用）。</param>
    /// <param name="numberOverride">覆盖卡面数字（抽/弃牌堆用它显示张数）。</param>
    internal void Bind(IntentCard? card, Creature owner, string? numberOverride = null)
    {
        if (!IsAlive)
        {
            return;
        }

        _card.Visible = true;

        Texture2D? art = null;
        var text = numberOverride ?? string.Empty;

        if (card is not null)
        {
            var move = IntentDeck.Resolve(owner, card);
            var intents = move?.Intents;
            var targets = MonsterAccess.Combat?.PlayerCreatures?.ToList() ?? [];
            art = FirstTexture(intents, targets, owner);

            if (numberOverride is null)
            {
                text = IntentNumbers.Text(intents, targets, owner);
            }
        }

        var portrait = _card.GetNodeOrNull<TextureRect>("%Portrait");
        if (portrait is not null)
        {
            // 模型驱动后卡图由实例的 PortraitPath 给出；art 非空时才覆盖（牌堆那种"没有对应意图"的才用得上）。
            if (art is not null)
            {
                portrait.Texture = art;
            }
        }

        if (_number is null)
        {
            if (!_loggedMissingNumber)
            {
                _loggedMissingNumber = true;
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 意图卡：卡面上找不到 %DescriptionLabel ⇒ 伤害数字不会显示");
            }

            return;
        }

        // 一律由视图写文字。
        // ⚠️ 2026-10-12 实测：模型层那条路暂时走不通 —— `CreateClone()` 在**规范实例**上被拒
        //（`CanonicalModelException: Canonical model ... used in incorrect place`），
        // 于是每张意图根本没拿到自己的实例 ⇒ 模型渲染不出数字。视图写是**当时唯一能出数**的方式。
        //（模型层的模板/参数已经留好，等找到"创建可变实例"的正确入口再切过去。）
        _number.Text = text;
        _number.Visible = text.Length > 0;
    }

    /// <summary>
    /// 把"鼠标进入/离开这张意图卡"上报给本体的瞄准管理器（`NTargetManager`）。
    /// </summary>
    /// <remarks>
    /// 这是"原版卡牌指向能点到意图卡"的**最后一环**：管理器本身支持非生物节点
    ///（<c>OnNodeHovered</c> 会对非 NCreature 的节点直接记 <c>HoveredNode</c>），
    /// 但节点得**自己上报**；生物是 <c>NCreature</c> 替它做的，我们的 <c>NCard</c> 没人管 ⇒ 这里补上。
    /// 只在"正在瞄准"时上报（管理器自己也有守卫，双保险）。
    /// </remarks>
    private void ConnectEngineHover()
    {
        _wrapper.MouseEntered += () =>
        {
            if (NTargetManager.Instance is { IsInSelection: true } manager)
            {
                manager.OnNodeHovered(_card);
                // 用户 2026-10-12：「指向高光怎么没有了」——
                // 换成原版指向后我们自己的悬停链路不再触发，所以在这里补上青色高光（离开时恢复基准高光）。
                SetHighlight(IntentHighlight.Selected);
            }
        };

        _wrapper.MouseExited += () =>
        {
            if (NTargetManager.Instance is { IsInSelection: true } manager)
            {
                manager.OnNodeUnhovered(_card);
                SetHighlight(_baseHighlight);
            }
        };
    }

    /// <summary>
    /// 可交互开关（指向模式打开时才为 true）：槽位节点接鼠标、真卡本身继续 Ignore（别让它抢焦点/点击）。
    /// </summary>
    internal void SetInteractable(bool on)
    {
        // 槽位始终 Stop（要靠它收悬停上报给本体瞄准）；这里的开关只影响"点它算不算数"的手感。
        _wrapper.MouseFilter = Control.MouseFilterEnum.Stop;
        _wrapper.MouseDefaultCursorShape = on ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow;
        SetMouseThrough(_card);
    }

    /// <summary>
    /// ⚠️ 关键：真卡**整棵子树**都要设成不吃鼠标（只设根节点不够 —— 它的子节点会把悬停/点击吃掉，
    /// 表现就是"卡带点不动、高光也收不回去"）。
    /// </summary>
    private static void SetMouseThrough(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = Control.MouseFilterEnum.Ignore;
        }

        foreach (var child in node.GetChildren())
        {
            SetMouseThrough(child);
        }
    }

    /// <summary>
    /// 指向模式：点一下 = 选中这张；悬停 = 给青色高光（用户 2026-10-12：
    /// "指向是使用卡牌选择目标，同选怪物一样，这里选的是意图卡牌"）。
    /// </summary>
    internal void SetTargeting(Action onClicked)
    {
        SetInteractable(true);

        _wrapper.MouseEntered += () => SetHighlight(IntentHighlight.Selected);
        _wrapper.MouseExited += () => SetHighlight(_baseHighlight);
        _wrapper.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                onClicked();
            }
        };
    }

    /// <summary>记住"非悬停时该显示哪种高光"（红=怪物将要打出的那张 / 无）。</summary>
    internal void SetBaseHighlight(IntentHighlight highlight)
    {
        _baseHighlight = highlight;
        SetHighlight(highlight);
    }

    /// <summary>灰化观感（不可行动 / 被虚无吃掉 / 虚无卡）。</summary>
    internal void SetDim(bool dim) =>
        _card.Modulate = dim ? new Color(0.55f, 0.55f, 0.6f, 1f) : Colors.White;

    /// <summary>
    /// 高光：<b>红</b> = 怪物将要打出的那张，<b>青</b> = 我们选中的那张，其它一律收起来。
    /// </summary>
    /// <remarks>
    /// 用本体 <c>NCard.CardHighlight</c> 那套（和手牌"能打出"的高光同一个节点），
    /// 所以观感与本体一致：只是换颜色 + 显示/隐藏。
    /// </remarks>
    internal void SetHighlight(IntentHighlight highlight)
    {
        if (!IsAlive)
        {
            return;
        }

        if (_highlight == highlight)
        {
            return;
        }

        _highlight = highlight;
        // 高光那张大 20%（青/红都是），鼠标移开后回到基准大小。
        ApplyScale(highlight == IntentHighlight.None ? 1f : EmphasisScale);

        var glow = _card.CardHighlight;
        if (glow is null)
        {
            return;
        }

        switch (highlight)
        {
            case IntentHighlight.Pending:
                glow.Modulate = NCardHighlight.red;
                glow.AnimShow();
                break;

            case IntentHighlight.Selected:
                glow.Modulate = SelectedColor;
                glow.AnimShow();
                break;

            default:
                glow.AnimHide();
                break;
        }
    }

    /// <summary>藏起来（交给 <see cref="NCard" /> 自己的池去回收前先把画面收干净）。</summary>
    internal void Hide()
    {
        if (IsAlive)
        {
            _card.Visible = false;
        }
    }

    internal void Free()
    {
        if (GodotObject.IsInstanceValid(_card))
        {
            _card.QueueFreeSafely();
        }
    }

    /// <summary>取该意图第一个能拿到的图（拿不到就返回 null，卡面留空）。</summary>
    private static Texture2D? FirstTexture(
        IReadOnlyList<AbstractIntent>? intents, IReadOnlyList<Creature> targets, Creature owner)
    {
        if (intents is null)
        {
            return null;
        }

        foreach (var intent in intents)
        {
            try
            {
                if (intent.GetTexture(targets, owner) is { } texture)
                {
                    return texture;
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 意图取图失败（跳过这张）：{ex.GetType().Name}");
            }
        }

        return null;
    }

}
