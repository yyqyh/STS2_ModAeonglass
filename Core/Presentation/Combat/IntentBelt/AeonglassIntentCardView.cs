using Godot;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Presentation.Art;
using STS2_Aeonglass.Core.Systems.Intent;
using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
/// <summary>一张「意图卡」的视图：<b>真卡外壳</b>（本体 <c>NCard</c>）+ 我们填进去的内容。</summary>
/// <remarks>
/// ① 职责：卡图 = 该意图自己的图；卡面只留动态数字；名字/描述/费用/类型全隐藏；按 <c>IntentHighlight</c> 上高光。
/// ② 调用时机：卡带 / 顶上选牌面板构建时由 <c>Create</c> 建。
/// ③ 依赖：意图的 <c>IntentCard</c> 模型 + 意图图 + <c>IntentHighlight</c>。
/// ④ ⚠️ 能"一个注册类撑起每张意图一张真卡"的前提：本体 <c>Title / PortraitPath</c> 是 virtual —— 本体若改可见性这里会塌。
/// </remarks>

internal sealed partial class AeonglassIntentCardView
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

    /// <summary>
    /// **卡面真身**节点的候选路径（本体画卡图的那一族；命中判定与调试黄框都以它为准）。
    /// </summary>
    /// <remarks>
    /// 为什么要认节点而不是认"卡的矩形"：卡节点本身的 <c>Size</c> 实测是 <c>(0,0)</c>（卡图是子节点画的），
    /// 所以"卡节点矩形"跟"看得见的卡面"根本不是一回事 —— 实测差一格（约 39 单位）+ 20 单位上下。
    /// </remarks>
    private static readonly string[] ArtNodePaths =
    [
        "%PortraitBorder",
        "%Portrait",
        "%PortraitCanvasGroup",
        "%Highlight",
    ];

    private readonly NCard _card;
    private readonly Control _wrapper;
    private readonly RichTextLabel? _number;

    /// <summary>卡面真身节点（<see cref="Create" /> 里解析一次；空 = 本体改了卡场景，走槽位兜底）。</summary>
    private readonly List<Control> _artNodes = [];

    private Vector2 _slotSize = Vector2.One;
    private Vector2 _baseScale = Vector2.One;

    /// <summary>
    /// 真卡的**基准位置**（不含悬停抬起）：让"真卡画出来的矩形中心"落在"槽位中心"。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 推导（2026-10-15 定稿，**解析式、不做测量**）：Godot 绕轴心缩放 ⇒
    /// 绘制矩形 = <c>Position + pivot·(1−s)</c> 起、大小 <c>卡面×s</c>；而 <c>pivot = 卡面/2</c> ⇒
    /// 绘制矩形中心 <c>= Position + pivot</c>（**与缩放无关**，也与倾斜无关 —— 倾斜绕中心转）。
    /// 要它落在槽位中心 ⇒ <c>Position = 槽位/2 − 卡面/2 = (槽位 − 卡面) / 2</c>。
    /// </para>
    /// <para>
    /// ⚠️ 减的是**未缩放的卡面尺寸**（<c>NCard.defaultSize</c> = 300×422），不是"卡面 × 缩放"。
    /// 早先写成 <c>(槽位 − 卡面×缩放)/2</c> ⇒ 卡面被 <c>pivot·(1−s)</c> ≈ (132,185) 顶到右下，
    /// "看到的卡"和"槽位命中框"分家（"指着卡点不中"的根因就在这）。
    /// </para>
    /// </remarks>
    private Vector2 _basePosition;

    /// <summary>是否已经"贴合槽位"过一次（尺寸没量到时为 false，下次悬停 / 换绑会补）。</summary>
    private bool _fitted;

    private bool _loggedPlacement;

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
    /// **指向判定的命中余量**（槽位本地单位，向四周外扩）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 高光态（1.2×）的卡面比槽位大：横向溢 3.6、纵向溢 4（左右上下对称）⇒ **4 就是"刚好盖住卡面"**，
    /// 再小会在卡的边缘露一条缝（"看着在卡上却不算"），再大就开始吃相邻卡的空隙。
    /// </para>
    /// </remarks>
    private const float HitMargin = 4f;

    /// <summary>
    /// **"看到的卡"相对槽位的额外偏移**（槽位本地单位）—— 表现与选中若有偏差就改这一行。
    /// </summary>
    /// <remarks>
    /// <para>默认 <c>(0, 0)</c>：卡面由 <see cref="_basePosition" /> 的解析式定位到槽位中心，两边同心，通常不用动。</para>
    /// <para>
    /// 真要微调（例：觉得卡面该再往右 2px）就改这里 —— **命中框会跟着一起挪**
    /// （<see cref="WrapperHitRect" /> 带这份偏移），所以"看到的卡 = 能点的卡"始终成立。
    /// </para>
    /// <para>X 正 = 右、Y 正 = 下；单位是槽位本地单位（屏幕位移 = 这个值 × 相机缩放）。</para>
    /// <para>⚠️ 它动的是**卡面 + 命中框一起**（观感会跟着动）。只想挪命中框见 <see cref="HitNudge" />。</para>
    /// </remarks>
    private static readonly Vector2 ArtNudge = new(0, 0);

    /// <summary>
    /// **命中框相对"节点几何"的偏移**（槽位本地单位）—— "我看到的卡"和"能点的框"对不上时填这里。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="ArtNudge" /> 的区别：`ArtNudge` 把卡面 + 命中框**一起**挪（观感会动）；
    /// 这个只挪**命中框**（卡面一动不动）→ "卡画在这儿、框在那儿"时用它对齐最省事、也最安全。
    /// </para>
    /// <para>
    /// 怎么算：屏幕上差几 px ÷ 相机缩放 = 这里的值（相机缩放 = "本地单位 → 屏幕像素"的比，
    /// 卡带就绪那行日志里的 <c>相机缩放=</c> 就是它）。<b>X 正 = 框往右、Y 正 = 框往下。</b>
    /// </para>
    /// <para>例：你看到"卡比框偏左 40、偏上 20"（本地单位）⇒ 填 <c>(40, 20)</c>，框就贴到卡上。</para>
    /// </remarks>
    private static readonly Vector2 HitNudge = new(0, 0);

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

    /// <summary>我们选中时的高光色（沙漏青）—— 唯一色源在 <see cref="AeonglassUiPalette.Cyan" />。</summary>
    private static Color SelectedColor => AeonglassUiPalette.Cyan;

    private AeonglassIntentCardView(NCard card, Control wrapper, RichTextLabel? number)
    {
        _card = card;
        _wrapper = wrapper;
        _number = number;
    }

    /// <summary>占位用的槽位节点（容器按它的最小尺寸排版；真卡缩放在里面，不影响布局）。</summary>
    internal Control Wrapper => _wrapper;

    /// <summary>解析卡面真身节点（见 <see cref="ArtNodePaths" />）；换绑模型后卡场景不变，所以只需一次。</summary>
    private void ResolveArtNodes()
    {
        _artNodes.Clear();

        if (!GodotObject.IsInstanceValid(_card))
        {
            return;
        }

        foreach (var path in ArtNodePaths)
        {
            if (_card.GetNodeOrNull<Control>(path) is { } node)
            {
                _artNodes.Add(node);
            }
        }
    }

    /// <summary>
    /// 把**卡面真身**的矩形中心挪到**槽位**中心 —— 让"看得见的卡"落在"Godot 能 pick 的那块矩形"里。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ 为什么必须这样做：卡面整棵子树 `MouseFilter = Ignore` ⇒ Godot 的 picking 只能落到**槽位**上，
    /// 于是"能不能选中"由槽位决定（实测：指卡面选不中、指槽位能选中）。所以唯一稳的解法是
    /// **把卡面摆进槽位**，而不是把判定挪到卡面上。
    /// </para>
    /// <para>
    /// 差值在视口坐标里量（卡面中心 vs 槽位中心），换算成槽位本地单位补进 <c>_card.Position</c>；
    /// 对齐后残差 ≈ 0 ⇒ 反复调用是幂等的（尺寸后到也没关系，下次悬停再补）。
    /// </para>
    /// </remarks>
    private void AlignArtToSlot()
    {
        if (!IsAlive || !GodotObject.IsInstanceValid(_wrapper) || _wrapper.Size.X <= 1f || _wrapper.Size.Y <= 1f)
        {
            return;
        }

        if (ActiveArtNode() is not { } art || art.Size.X <= 1f || art.Size.Y <= 1f)
        {
            return;
        }

        var slotXform = _wrapper.GetGlobalTransformWithCanvas();
        var scale = slotXform.Scale;
        if (Mathf.Abs(scale.X) < 0.0001f || Mathf.Abs(scale.Y) < 0.0001f)
        {
            return;
        }

        var slotCenter = slotXform * (_wrapper.Size * 0.5f);
        var artCenter = art.GetGlobalTransformWithCanvas() * (art.Size * 0.5f);
        var delta = new Vector2((slotCenter.X - artCenter.X) / scale.X, (slotCenter.Y - artCenter.Y) / scale.Y);

        if (_hovered)
        {
            // 抬起（HoverLift）是额外位移：量的时候先抵掉，别把它吃进基准位置。
            delta.Y -= HoverLift;
        }

        if (delta.LengthSquared() < 0.01f)
        {
            _fitted = true;
            return;
        }

        _card.Position += delta;
        _basePosition = _card.Position + new Vector2(0f, _hovered ? HoverLift : 0f);
        _fitted = true;

        // 表现层诊断（每张卡一次）⇒ 归「详细诊断日志」开关。
        if (!_loggedPlacement && AeonglassSettingStore.DebugVerboseLogs)
        {
            _loggedPlacement = true;
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 卡面对齐槽位：卡面节点={art.Name} {art.Size}"
                + $"｜补位={delta}（卡面中心 → 槽位中心）｜基准={_basePosition}");
        }
    }

    /// <summary>卡节点（布局 / 点击都直接用它）。</summary>
    internal NCard Card => _card;

    internal Vector2 SlotSize => _slotSize;

    /// <summary>移动动画副本：无鼠标命中、无悬停，不参与卡带布局。</summary>
    internal void SetGhostMode()
    {
        _wrapper.MouseFilter = Control.MouseFilterEnum.Ignore;
        _wrapper.ZIndex = 200;
        _card.MouseFilter = Control.MouseFilterEnum.Ignore;
        SetInteractable(false);
        SetHovered(false);
    }

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
            view.ResolveArtNodes();
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
        var raw = RawCardSize();
        if (raw.X <= 1f || raw.Y <= 1f || slotSize.X <= 1f || slotSize.Y <= 1f)
        {
            return;
        }

        // ⚠️ 用**未缩放**的卡面尺寸算缩放：`GetCurrentSize()` 是 `defaultSize × 自己的 Scale`，
        //    再算一遍会把已经缩过的尺寸当成原始尺寸（第二次调用就会把卡放大回全尺寸）。
        var scale = Mathf.Min(slotSize.X / raw.X, slotSize.Y / raw.Y);
        _slotSize = slotSize;
        _baseScale = new Vector2(scale, scale);

        // 悬停要轻微倾斜：轴心设在卡面中心（不设的话会绕左上角转，像"翻页"而不是"抬手牌"）。
        _card.PivotOffset = raw * 0.5f;

        // 基准位置：解析式（见 _basePosition 的推导）—— 绘制矩形中心 = Position + 轴心 = 槽位中心。
        // 加上玩家的 ArtNudge 微调（命中框带同一份偏移 ⇒ "看到的卡 = 能点的框"始终成立）。
        _basePosition = ((slotSize - raw) * 0.5f) + ArtNudge;
        _fitted = true;
        ApplyScale(1f);
        // 解析式摆的是"卡节点矩形"，但真正看得见的卡面是子节点画的 ⇒ 再实测把**卡面**摆到槽位中心
        //（必须放在 ApplyScale 之后：ApplyScale 会用 _basePosition 覆写 Position）。
        AlignArtToSlot();

        if (!_loggedPlacement)
        {
            _loggedPlacement = true;
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 意图卡定位（解析式）：槽位={slotSize} 卡面(未缩放)={raw} "
                + $"缩放={scale:0.###} 基准={_basePosition} 轴心={_card.PivotOffset}｜绘制矩形中心 = 基准 + 轴心 = 槽位中心");
        }

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

        _card.Scale = _baseScale * factor * RevealScale();
        // 轴心在卡面中心 ⇒ 缩放/倾斜都是"绕中心"，中心恒在 基准 + 轴心；悬停抬起是额外的一点点位移。
        // 发牌显现的上浮偏移也要一起进位置表达式，否则悬停补间会把上浮吃掉。
        _card.Position = _basePosition + RevealOffset() + new Vector2(0f, _hovered ? -HoverLift : 0f);
    }


    /// <summary>卡面的**未缩放**尺寸（`NCard.GetCurrentSize()` 是"未缩放尺寸 × 自己的 Scale"，不能当原始尺寸用）。</summary>
    private Vector2 RawCardSize() =>
        _card.Size.X > 1f && _card.Size.Y > 1f ? _card.Size : NCard.defaultSize;

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

    /// <summary>
    /// 鼠标现在是否压在这张卡上。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用 <c>Control.GetLocalMousePosition()</c> 换算（Godot 自己会把父级缩放/位移、画布变换都算进去），
    /// 比"自己拿 <c>GetMousePosition()</c> 去比 <c>GetGlobalRect()</c>"稳 —— 卡带在受相机缩放的子树里，
    /// 两个空间一旦不一致命中测试就会永远落空。
    /// </para>
    /// <para>
    /// 两道判定都算命中：① 槽位矩形外扩 <see cref="HitMargin" />；② 真卡自己的矩形（悬停时会抬起/放大/倾斜，
    /// 它自己的局部坐标已经把那些变换算进去了）。
    /// </para>
    /// </remarks>
    internal bool ContainsMouse()
    {
        if (!GodotObject.IsInstanceValid(_wrapper) || !_wrapper.Visible)
        {
            return false;
        }

        // ⚠️ **只认槽位**（2026-10-15 实测定案）：卡面那棵子树是 `MouseFilter=Ignore`，Godot 的 picking
        // 永远打不到卡面节点，只能落到槽位上 ⇒ "能不能选中"实际由**槽位这块矩形**决定
        //（实测：指卡面选不中、指槽位能选中）。所以命中区以槽位为准，卡面由 <see cref="AlignArtToSlot" /> 摆进槽位里。
        return WrapperHitRect().HasPoint(_wrapper.GetLocalMousePosition());
    }

    /// <summary>
    /// 当前用来判定的**卡面节点**：优先"可见且尺寸有效"，其次"尺寸有效"，都没有则 null。
    /// </summary>
    /// <remarks>
    /// ⚠️ 必须和描边用同一个节点：`%PortraitBorder` 被我们**隐藏**了（`HiddenChrome`），
    /// 它的矩形和真正看得见的 `%Portrait` 不保证重合 —— 描边拿到隐藏节点就会出现"框贴住了、判定在别处"。
    /// </remarks>
    private Control? ActiveArtNode()
    {
        Control? sized = null;
        foreach (var art in _artNodes)
        {
            if (!GodotObject.IsInstanceValid(art) || art.Size.X <= 1f || art.Size.Y <= 1f)
            {
                continue;
            }

            if (art.Visible)
            {
                return art;
            }

            sized ??= art;
        }

        return sized;
    }

    /// <summary>槽位本地空间里的命中矩形（= 槽位 + <see cref="ArtNudge" /> + <see cref="HitNudge" /> + <see cref="HitMargin" /> 外扩）。</summary>
    private Rect2 WrapperHitRect() =>
        new Rect2(ArtNudge + HitNudge, _wrapper.Size).Grow(HitMargin);

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

        // ⚠️ 新模型可能换了一副卡面尺寸 ⇒ 重新贴合槽位 + 重新对齐（对齐是**实测**的，不依赖旧尺寸）。
        ResizeTo(_slotSize);
        ApplyScale(1f);
        _card.Rotation = 0f;
        Bind(card, owner, numberOverride);
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

            // ⚠️ 上报只限"正在打我们的指向卡"：别人的瞄准（攻击 / 指队友 / 自身）一点不碰。
            if (IntentTargetRegistry.AimingOurCard
                && NTargetManager.Instance is { IsInSelection: true } manager)
            {
                manager.OnNodeHovered(_card);
            }
        };

        _wrapper.MouseExited += () =>
        {
            SetHovered(false);

            if (IntentTargetRegistry.AimingOurCard
                && NTargetManager.Instance is { IsInSelection: true } manager)
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
            // ⚠️ 同一档不重做；但"**无高光**"这一档必须每次确保真的收起 ——
            //    本体 `%Highlight.AnimFlash()`（发牌红闪用它）会把背光**留在显示状态**，
            //    只靠"档位没变就 return"收不回来 ⇒ 实测"发牌结束后全部是红背光"。
            if (highlight == IntentHighlight.None
                && _card.CardHighlight is { } stuck
                && (stuck.Visible || stuck.Modulate.A > 0.01f))
            {
                stuck.AnimHide();
            }

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
