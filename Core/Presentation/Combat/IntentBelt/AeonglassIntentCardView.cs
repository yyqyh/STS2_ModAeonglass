using Godot;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

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

    // ==================== 悬停表现（2026-10-15 用户口径：全套真卡式）====================

    /// <summary>悬停时的放大倍率（本体手牌口径是 1.1）。</summary>
    private const float HoverFactor = 1.1f;

    /// <summary>"强调（红=将打出 / 青=选中）"与"悬停"同时命中时的倍率。</summary>
    private const float HoverPlusEmphasisFactor = 1.25f;

    /// <summary>悬停时上抬多少（卡带局部单位；与 <see cref="ApplyScale" /> 同一口径）。</summary>
    private const float HoverLift = 6f;

    /// <summary>悬停时的倾角（度）—— 按槽位左右分正负，做出手牌扇形的错觉。</summary>
    private const float HoverTiltDeg = 3f;

    /// <summary>进入 / 退出时长（秒）：进得快、出得慢（0.5 s Expo-out 照本体手牌口径）。</summary>
    private const float HoverInSeconds = 0.08f;
    private const float HoverOutSeconds = 0.5f;

    /// <summary>悬停时抬到最前，避免被右邻卡遮住。</summary>
    private const int HoverZIndex = 10;

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

    /// <summary>这张槽位当前绑的是哪张意图（槽位复用靠它比较；null = 空槽）。</summary>
    private IntentCard? _identity;

    /// <summary>槽位序号 / 本行槽位数（决定悬停倾斜的方向）。</summary>
    private int _slotIndex;
    private int _slotCount = 1;

    private bool _hovered;
    private Tween? _visualTween;

    /// <summary>悬停用的**金色背光**（第二层 <c>%Highlight</c>；材质是深拷贝，和原层互不干扰）。</summary>
    private NCardHighlight? _hoverLayer;

    /// <summary>指向模式的点击回调（只接一次线 ⇒ 反复调 <see cref="SetTargeting" /> 不会叠加 handler）。</summary>
    private Action? _onClicked;
    private bool _clickWired;

    private bool? _lastDim;
    private string? _lastNumber;
    private bool? _lastInteractable;
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

        // 悬停要轻微倾斜：轴心设在卡面中心（不设的话会绕左上角转，像"翻页"而不是"抬手牌"）。
        _card.PivotOffset = current * 0.5f;

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
        // ⚠️ 位置**不乘 factor**：卡的轴心（<c>PivotOffset</c>）设在卡面中心 ⇒ 缩放是"绕中心变大"，
        //    视觉中心恒在 <c>position + baseSize/2</c>。若这里再按 factor 反算一次，放大时卡会往左上漂
        //    （实测就是"悬停后位置不对"）。
        _card.Position = (_slotSize - _baseSize) * 0.5f;
    }

    // ==================== 悬停：金色背光 + 放大 + 抬起 + 轻微倾斜 ====================

    /// <summary>
    /// 造一层金色背光：**复制本体那个 <c>%Highlight</c> 节点**（自带 shader 与 AnimShow/AnimHide），
    /// 并给它一份**独立但不深拷**的 <c>ShaderMaterial</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ① 必须在<b>第一次悬停时</b>才建（不在创建卡视图时建）：复制材质 + 首帧上屏的开销挪出"进战斗"那一帧，
    /// 进战斗不再因为这条装饰而卡一下。
    /// </para>
    /// <para>
    /// ② <c>Duplicate()</c> 用**默认参数**（不深拷子资源）：材质实例独立 ⇒ 两层的 <c>width</c> 参数互不干扰，
    /// 但 <c>Shader</c> 本体共享 ⇒ 不会给每张卡多编译一份 shader 管线（`Duplicate(true)` 会，那正是卡顿的来源之一）。
    /// </para>
    /// </remarks>
    private void EnsureHoverBacklight()
    {
        if (_hoverLayer is { } existing && GodotObject.IsInstanceValid(existing))
        {
            return;
        }

        if (_card.CardHighlight is not { } source || source.GetParent() is not { } parent)
        {
            return;
        }

        try
        {
            var layer = (NCardHighlight)source.Duplicate();
            if (layer.Material is ShaderMaterial shared)
            {
                layer.Material = (ShaderMaterial)shared.Duplicate();
            }

            layer.Name = "HoverBacklight";
            layer.Modulate = NCardHighlight.gold;
            layer.MouseFilter = Control.MouseFilterEnum.Ignore;

            parent.AddChildSafely(layer);
            parent.MoveChild(layer, Math.Min(source.GetIndex() + 1, parent.GetChildCount() - 1));
            layer.AnimHideInstantly();

            _hoverLayer = layer;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 悬停背光层创建失败（只影响悬停观感）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>槽位序号（每行重排后由卡带告知，用于决定倾斜方向）。</summary>
    internal void SetSlot(int index, int count)
    {
        _slotIndex = index;
        _slotCount = Math.Max(1, count);

        if (_hovered)
        {
            AnimateToTarget();
        }
    }

    /// <summary>
    /// 悬停开 / 关：金色背光（淡入淡出）+ 放大 + 抬起 + 轻微倾斜，全部走**同一条 Tween**。
    /// </summary>
    /// <remarks>
    /// 三档语义互不覆盖：红（将打出）/ 青（我们选中）走本体的 <c>%Highlight</c> 原层，
    /// 悬停走这里复制出来的金色层 —— 所以"将打出 + 悬停"两处光可以同时亮着。
    /// </remarks>
    internal void SetHovered(bool hovered)
    {
        if (!IsAlive || _hovered == hovered)
        {
            return;
        }

        _hovered = hovered;
        _wrapper.ZIndex = hovered ? HoverZIndex : 0;

        // 背光层是**第一次悬停才建**的（把复制材质/首次上屏的成本挪出"进战斗"那一帧）。
        if (hovered)
        {
            EnsureHoverBacklight();
        }

        if (_hoverLayer is { } layer && GodotObject.IsInstanceValid(layer))
        {
            if (hovered)
            {
                layer.AnimShow();
            }
            else
            {
                layer.AnimHide();
            }
        }

        if (hovered)
        {
            ShowHoverTips();
        }
        else
        {
            NHoverTipSet.Remove(_wrapper);
        }

        AnimateToTarget();
    }

    /// <summary>悬停时弹卡面自带的 tips（模型没有 tips 就不弹；不新增本地化键）。</summary>
    private void ShowHoverTips()
    {
        try
        {
            var tips = _card.Model?.HoverTips;
            if (tips is null || !tips.Any())
            {
                return;
            }

            NHoverTipSet.CreateAndShow(_wrapper, tips);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} 意图 tips 失败（已忽略）：{ex.GetType().Name}");
        }
    }

    /// <summary>当前应有的放大倍率：强调（红/青）与悬停取较大者，两者同时命中再叠一点。</summary>
    private float CurrentFactor()
    {
        var emphasis = _highlight != IntentHighlight.None;
        return (emphasis, _hovered) switch
        {
            (true, true) => HoverPlusEmphasisFactor,
            (true, false) => EmphasisScale,
            (false, true) => HoverFactor,
            _ => 1f,
        };
    }

    /// <summary>悬停倾斜：左半往左倒、右半往右倒（像摊开的手牌）。</summary>
    private float CurrentTiltDeg()
    {
        if (!_hovered || _slotCount <= 1)
        {
            return 0f;
        }

        var middle = (_slotCount - 1) * 0.5f;
        var offset = _slotIndex - middle;
        return Mathf.Sign(offset) * HoverTiltDeg;
    }

    /// <summary>把"缩放 / 抬起 / 倾斜"三个目标值一次性补间过去（进快出慢）。</summary>
    private void AnimateToTarget()
    {
        if (!IsAlive)
        {
            return;
        }

        var factor = CurrentFactor();
        var targetScale = _baseScale * factor;
        // 同样：轴心在中心 ⇒ 位置只按"未缩放大小"居中，抬起是额外的一点点位移。
        var targetPosition = ((_slotSize - _baseSize) * 0.5f) + new Vector2(0f, _hovered ? -HoverLift : 0f);
        var targetRotation = Mathf.DegToRad(CurrentTiltDeg());
        var seconds = _hovered ? HoverInSeconds : HoverOutSeconds;

        _visualTween?.Kill();
        _visualTween = _wrapper.CreateTween().SetParallel();
        _visualTween.TweenProperty(_card, "scale", targetScale, seconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        _visualTween.TweenProperty(_card, "position", targetPosition, seconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        _visualTween.TweenProperty(_card, "rotation", targetRotation, seconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
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
        _identity = card;

        Texture2D? art = null;

        if (card is not null)
        {
            var move = IntentCycle.Resolve(owner, card);
            var intents = move?.Intents;
            var targets = MonsterAccess.Combat?.PlayerCreatures?.ToList() ?? [];
            art = FirstTexture(intents, targets, owner);
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

        // 数字一律走"变了才写"的那条路（槽位复用后每次刷新都会调它）。
        _lastNumber = null;
        RefreshFace(card, owner, numberOverride);
    }

    /// <summary>这张槽位当前绑的意图（卡带用它判断"要不要换模型"）。</summary>
    internal IntentCard? Identity => _identity;

    /// <summary>挂进（或挪回）卡带的槽位行 —— 复用同一个视图时按顺序调用它即可。</summary>
    internal void AttachTo(Node parent)
    {
        if (!IsAlive)
        {
            return;
        }

        if (_wrapper.GetParent() != parent)
        {
            _wrapper.GetParent()?.RemoveChildSafely(_wrapper);
            parent.AddChildSafely(_wrapper);
        }

        // 复用：把占位尺寸与可见性都还原回来。
        _wrapper.Visible = true;
        _wrapper.CustomMinimumSize = _slotSize;
    }

    /// <summary>
    /// 这次用不到它：收掉悬停/画面、占位清零。
    /// </summary>
    /// <remarks>
    /// ⚠️ **不要**把它从父节点上摘下来 —— 那样它就成了游离节点，卡带销毁时不会被一起释放（等于漏节点）。
    /// 留在树里 + 最小尺寸归零 ⇒ HBox 不给它留位置，观感与"撤下来"一致，还省掉一次重挂。
    /// </remarks>
    internal void Park()
    {
        if (!GodotObject.IsInstanceValid(_wrapper))
        {
            return;
        }

        SetHovered(false);
        Hide();
        _wrapper.Visible = false;
        _wrapper.CustomMinimumSize = Vector2.Zero;
    }

    /// <summary>
    /// 把同一个槽位**换绑**到另一张意图：重设卡的模型（本体 setter 会发 <c>ModelChanged</c>）后重跑
    /// <see cref="Bind" />。节点不重建 ⇒ 只有"这一格的牌变了"才付这份成本。
    /// </summary>
    internal void Rebind(IntentCard? card, Creature owner, CardModel? model, string? numberOverride = null)
    {
        if (!IsAlive)
        {
            return;
        }

        try
        {
            if (model is not null && !ReferenceEquals(_card.Model, model))
            {
                _card.Model = model;
            }
        }
        catch (Exception ex)
        {
            // 换模型失败就沿用旧模型（观感差一点，但绝不影响机制）。
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 换绑卡模型失败（沿用旧模型）：{ex.GetType().Name}: {ex.Message}");
        }

        // 换了卡 ⇒ 高光与数字都要重算一遍（同一档位也要"重新应用"）。
        // ⚠️ 视觉也要回零：不能让"上一张的悬停态（放大 + 金色背光 + 倾斜）"带给了下一张。
        SetHovered(false);
        _highlight = IntentHighlight.None;
        _visualTween?.Kill();
        _visualTween = null;
        ApplyScale(1f);
        _card.Rotation = 0f;
        Bind(card, owner, numberOverride);
    }

    /// <summary>退出指向模式：撤掉点击回调与手型光标（否则不在指向模式里点卡也会提交一次意图操作）。</summary>
    internal void ClearTargeting()
    {
        _onClicked = null;
        SetInteractable(false);
    }

    /// <summary>
    /// 只刷卡面（同一张意图、只有数字可能变）：文本没变就什么都不做。
    /// </summary>
    internal void RefreshFace(IntentCard? card, Creature owner, string? numberOverride = null)
    {
        var text = NumberText(card, owner, numberOverride);
        if (_lastNumber == text)
        {
            return;
        }

        _lastNumber = text;

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

    /// <summary>算这一张该显示的数字文本（覆盖值优先；没有对应意图就是空）。</summary>
    private static string NumberText(IntentCard? card, Creature owner, string? numberOverride)
    {
        if (numberOverride is not null)
        {
            return numberOverride;
        }

        if (card is null)
        {
            return string.Empty;
        }

        var move = IntentCycle.Resolve(owner, card);
        var targets = MonsterAccess.Combat?.PlayerCreatures?.ToList() ?? [];
        return IntentNumbers.Text(move?.Intents, targets, owner);
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
            // 悬停表现（金色背光 + 放大 + 抬起 + 倾斜）与"上报给本体瞄准"是两件事，互不依赖。
            SetHovered(true);

            if (NTargetManager.Instance is { IsInSelection: true } manager)
            {
                manager.OnNodeHovered(_card);
            }
        };

        _wrapper.MouseExited += () =>
        {
            SetHovered(false);

            if (NTargetManager.Instance is { IsInSelection: true } manager)
            {
                manager.OnNodeUnhovered(_card);
            }
        };
    }

    /// <summary>
    /// 可交互开关（指向模式打开时才为 true）：槽位节点接鼠标、真卡本身继续 Ignore（别让它抢焦点/点击）。
    /// </summary>
    internal void SetInteractable(bool on)
    {
        if (_lastInteractable == on)
        {
            return;
        }

        _lastInteractable = on;
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
        _onClicked = onClicked;

        if (_clickWired)
        {
            return;
        }

        _clickWired = true;
        _wrapper.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _onClicked?.Invoke();
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
    internal void SetDim(bool dim)
    {
        if (!IsAlive || _lastDim == dim)
        {
            return;
        }

        _lastDim = dim;
        _card.Modulate = dim ? new Color(0.55f, 0.55f, 0.6f, 1f) : Colors.White;
    }

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
        // 高光那张大 20%（青/红都是）；悬停与它叠加时走另一档（见 CurrentFactor），两条路都补间。
        AnimateToTarget();

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
