using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// M2 表现层 · 第 2 步 **2-a：静态传送门**（左右各一道，贴在"间隙"显示牌堆容器两端）。
/// </summary>
/// <remarks>
/// <para>
/// <b>门的"长相"全在场景里</b>：<c>Const.Paths.Ui.GapPortalScene</c>
/// （<c>res://STS2_Aeonglass/scenes/ui/gap/aeonglass_gap_portal.tscn</c>）——
/// 三层贴图（<c>Ray</c> 外圈 / <c>Smoke</c> 烟雾 / <c>Glob</c> 内核心）+ 染色 +
/// 一个 <c>AnimationPlayer</c>（<c>breath</c> 已自动播放；<c>ray_spin</c> 备着，把 autoplay 改成它就能开）。
/// 本文件<b>只负责"摆在哪"</b>：实例化两份、跟着间隙容器定位。
/// </para>
/// <para>
/// <b>改视觉</b> ⇒ 改 <c>.tscn</c>（或直接用 Godot 编辑器打开），改完<b>必须重导 PCK</b>
/// （构建时 <c>-p:RunPckExport=true</c>，或手动 <c>--export-pack BasicExport</c>）；
/// <b>改位置 / 缩放</b> ⇒ 改下面那一块常量，重编译 DLL 即可。
/// </para>
/// <para>
/// <b>挂法</b>：本节点挂在**战斗 UI** 下（与额外手牌容器同级 ⇒ 坐标同源），每 0.25s 找到容器、
/// 把自己那两道门定位到容器左右两端。**不往容器里塞子节点**（那是它自己的布局区，塞进去会被摆走）。
/// </para>
/// </remarks>
public partial class GapPortals : Control
{
    // ==================== 可调参数（改这里就行） ====================

    /// <summary>门与容器左右端之间的空隙（像素，正=更靠外）。</summary>
    private const float SideMargin = 350f;

    /// <summary>门相对"容器竖直中心"的偏移（正=向下，负=向上）。</summary>
    private const float VerticalOffset = 0f;

    /// <summary>门的整体缩放（1 = 场景里的原始大小；以门中心为锚点缩放）。</summary>
    private const float PortalScale = 1f;

    /// <summary>是否在容器右侧也显示一道门（关掉 = 只留左边那道）。</summary>
    private const bool ShowRightPortal = true;

    /// <summary>
    /// 门是否**常驻显示**。
    /// </summary>
    /// <remarks>
    /// <c>false</c>（默认，用户 2026-10-12 口径）= <b>只在动画期间出现</b>：平时整道门是隐藏的，
    /// "开启"时淡入 + 张开，"消失"时缩掉 + 淡出。位置参数照旧一直有效，只是看不见而已。
    /// 想看常驻效果就把这个改成 <c>true</c>。
    /// </remarks>
    private const bool AlwaysVisible = false;

    /// <summary>"没张开"时门缩到多小（只在 <see cref="AlwaysVisible" /> 为 false 时用）。</summary>
    private const float HiddenScale = 0.55f;

    // ===== 开 / 合（2-b 的坍缩动画会调这两个）=====

    /// <summary>开门后**门本身**的缩放（相对常态）。</summary>
    private const float OpenScale = 1.18f;

    /// <summary>开门闪光环：最终放大到门的多少倍。</summary>
    private const float OpenFlashScale = 1.95f;

    /// <summary>开门闪光环的起始不透明度。</summary>
    private const float OpenFlashAlpha = 0.8f;

    // ================================================================

    /// <summary>重新找容器 / 重新对位的间隔（秒）。</summary>
    private const float RescanInterval = 0.25f;

    /// <summary>
    /// 开门/闪光环用的青色。<b>门本体的染色在 <c>.tscn</c> 里</b>（那份才算数），
    /// 这里这份只给"开门闪光环"用 —— 换配色时两边一起改，保持一致。
    /// </summary>
    private static readonly Color PortalTint = new("7FE7DD");

    private PackedScene? _scene;
    private NModExtraHand? _container;
    private Control? _left;
    private Control? _right;
    private Tween? _leftTween;
    private Tween? _rightTween;
    private readonly List<(CanvasItem Layer, Color BaseTint)> _leftLayers = [];
    private readonly List<(CanvasItem Layer, Color BaseTint)> _rightLayers = [];
    private float _timer;
    private bool _loggedMount;

    /// <summary>当前战斗里已挂载的那一份（表现层动画靠它取门的位置 / 播开合）。</summary>
    internal static GapPortals? Instance { get; private set; }

    /// <summary>挂进战斗 UI（由 <c>CombatUiMountPatch</c> 调）。</summary>
    internal static void Attach(Node room)
    {
        AeonglassSettingStore.Initialize();

        var setting = AeonglassSettingStore.Current;
        if (!setting.GapEnabled)
        {
            return;
        }

        if (room is not MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom combatRoom
            || combatRoom.Ui is not { } ui)
        {
            return;
        }

        ui.AddChild(new GapPortals
        {
            Name = nameof(GapPortals),
            ZIndex = -2, // 压在额外手牌容器（-1）之后，避免门盖住卡面
        });
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Instance = this;

        _scene = LoadPortalScene();
        if (_scene is null)
        {
            SetProcess(false);
            return;
        }

        SetProcess(true);
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }

    public override void _Process(double delta)
    {
        // 目标选取期间让额外手牌区不挡鼠标（群友 2026-10-13 指出的那个坑）。
        // 每帧只做一次 bool 比较，只在"在选目标/选完"变化时才真正扫 holder 改 Hitbox。
        ExtraHandRefresh.SyncMouseThrough();

        _timer += (float)delta;
        if (_timer < RescanInterval)
        {
            return;
        }

        _timer = 0f;

        if (_container is null || !IsInstanceValid(_container))
        {
            _container = FindContainer();
            if (_container is not null)
            {
                EnsurePortals();
            }
        }

        SyncPosition();
    }

    /// <summary>取传送门场景（取不到就报一次错并停掉本节点，不静默失败）。</summary>
    private static PackedScene? LoadPortalScene()
    {
        var path = Const.Paths.Ui.GapPortalScene;

        try
        {
            if (PreloadManager.Cache.GetScene(path) is { } scene)
            {
                return scene;
            }
        }
        catch (Exception e)
        {
            Const.Logger.Error($"{Const.Prefix.Gap} 传送门场景加载抛异常：{path} —— {e.Message}");
            return null;
        }

        Const.Logger.Error(
            $"{Const.Prefix.Gap} 传送门场景没加载出来：{path} —— 检查它是否被打进 PCK（改完 .tscn 要重导 PCK）。");
        return null;
    }

    /// <summary>找"间隙"的显示牌堆容器（按显示牌堆 id 匹配）。</summary>
    private static NModExtraHand? FindContainer()
    {
        if (NCombatRoomOf()?.Ui is not { } ui)
        {
            return null;
        }

        var target = GapView.ZoneId;
        return ScanFor(ui, target, 0);
    }

    private static MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom? NCombatRoomOf() =>
        MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance;

    private static NModExtraHand? ScanFor(Node root, string pileId, int depth)
    {
        if (depth > 6)
        {
            return null;
        }

        foreach (var child in root.GetChildren())
        {
            if (child is NModExtraHand hand
                && string.Equals(hand.Definition?.Id, pileId, StringComparison.OrdinalIgnoreCase))
            {
                return hand;
            }

            if (ScanFor(child, pileId, depth + 1) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    /// <summary>创建左右两道门（幂等）。</summary>
    private void EnsurePortals()
    {
        _left ??= BuildPortal("PortalLeft", _leftLayers);

        if (ShowRightPortal && _right is null)
        {
            _right = BuildPortal("PortalRight", _rightLayers);
        }

        if (_left is not null && !_loggedMount)
        {
            _loggedMount = true;
            Const.Logger.Info(
                $"{Const.Prefix.Gap} 传送门已挂载（场景={Const.Paths.Ui.GapPortalScene}，右门={(ShowRightPortal ? "开" : "关")}）。");
        }
    }

    /// <summary>
    /// 一道门 = 实例化一次场景（视觉全在场景里，这里只挂上去 + 定缩放）。
    /// 顺手把各图层的**常态染色**记进 <paramref name="layers" />（开/合的淡入淡出要用），
    /// 并在"非常驻显示"模式下把它们先抹成全透明。
    /// </summary>
    private Control? BuildPortal(string name, List<(CanvasItem Layer, Color BaseTint)> layers)
    {
        // 场景有可能在跑局中途被本体的资源卸载回收掉 ⇒ 取用前先验一次。
        if (_scene is null || !IsInstanceValid(_scene))
        {
            return null;
        }

        var portal = _scene.Instantiate<Control>(PackedScene.GenEditState.Disabled);
        portal.Name = name;
        portal.MouseFilter = MouseFilterEnum.Ignore;
        portal.Scale = Vector2.One * PortalScale;
        portal.Visible = AlwaysVisible;

        AddChild(portal);

        layers.Clear();
        foreach (var child in portal.GetChildren())
        {
            if (child is not CanvasItem layer)
            {
                continue; // AnimationPlayer 之类不是画布元素，跳过
            }

            var baseTint = layer.Modulate;
            layers.Add((layer, baseTint));
            if (!AlwaysVisible)
            {
                layer.Modulate = ClosedTint(baseTint);
            }
        }

        return portal;
    }

    /// <summary>
    /// 场景里的"设计尺寸"（定位按它算；门以自身中心为锚点缩放，所以缩放不挪动中心点）。
    /// </summary>
    private static Vector2 DesignSizeOf(Control portal)
    {
        var min = portal.CustomMinimumSize;
        return min.X > 0f && min.Y > 0f ? min : portal.Size;
    }

    /// <summary>把自己定位到容器左右两端（与容器同级 ⇒ 同一坐标空间）。</summary>
    private void SyncPosition()
    {
        if (_container is null || !IsInstanceValid(_container)
            || _left is null || !IsInstanceValid(_left))
        {
            return;
        }

        var design = DesignSizeOf(_left);
        var centerY = _container.Position.Y + (_container.Size.Y - design.Y) * 0.5f + VerticalOffset;

        _left.Position = new Vector2(_container.Position.X - design.X - SideMargin, centerY);
        if (_right is not null && IsInstanceValid(_right))
        {
            _right.Position = new Vector2(_container.Position.X + _container.Size.X + SideMargin, centerY);
        }
    }

    // ==================== 给表现层（GapSequence）用的门 API ====================

    /// <summary>
    /// 取某一道门的**中心**（全局坐标 —— 与卡节点、战斗 UI 同一坐标空间）。
    /// </summary>
    /// <param name="preferRight">优先要右门；右门没建（<see cref="ShowRightPortal" /> 关了）就退到左门。</param>
    /// <param name="center">门中心；失败时是 <c>default</c>。</param>
    /// <returns>取到了返回 true。取不到（门还没建 / 已被释放）返回 false，调用方该退回本体表现。</returns>
    internal bool TryGetPortalCenter(bool preferRight, out Vector2 center)
    {
        var portal = preferRight ? _right ?? _left : _left ?? _right;
        if (portal is null || !IsInstanceValid(portal))
        {
            center = default;
            return false;
        }

        // 门以自身中心为锚点缩放（见 DesignSizeOf 的说明）⇒ 全局位置 + 半个设计尺寸就是中心。
        center = portal.GlobalPosition + DesignSizeOf(portal) * 0.5f;
        return true;
    }

    /// <summary>开门：门**出现**（淡入 + 从小张开）+ 套一层"张开"的闪光环；await 到动画结束。</summary>
    internal async Task PlayOpenAsync(bool right, float seconds)
    {
        var portal = Portal(right);
        if (portal is null)
        {
            return;
        }

        var layers = Layers(right);
        KillPortalTween(right);

        // 从"没张开"的状态起步（常驻模式下这一对就是常态值 ⇒ 等价于原来那个放大脉冲）
        portal.Visible = true;
        portal.Scale = Vector2.One * ClosedScale;
        foreach (var (layer, baseTint) in layers)
        {
            layer.Modulate = ClosedTint(baseTint);
        }

        SpawnOpenFlash(portal, seconds);

        var tween = portal.CreateTween().SetParallel();
        SetPortalTween(right, tween);
        tween.TweenProperty(portal, "scale", Vector2.One * (PortalScale * OpenScale), seconds)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        foreach (var (layer, baseTint) in layers)
        {
            tween.TweenProperty(layer, "modulate", baseTint, seconds * 0.85f);
        }

        await tween.AwaitFinished(this);
    }

    /// <summary>关门：缩掉 + 淡出（非常驻模式下关门后就整道门不可见了）。</summary>
    internal async Task PlayCloseAsync(bool right, float seconds)
    {
        var portal = Portal(right);
        if (portal is null)
        {
            return;
        }

        var layers = Layers(right);
        KillPortalTween(right);

        var tween = portal.CreateTween().SetParallel();
        SetPortalTween(right, tween);
        tween.TweenProperty(portal, "scale", Vector2.One * ClosedScale, seconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        foreach (var (layer, baseTint) in layers)
        {
            tween.TweenProperty(layer, "modulate", ClosedTint(baseTint), seconds);
        }

        tween.Chain().TweenCallback(Callable.From(() => portal.Visible = AlwaysVisible));

        await tween.AwaitFinished(this);
    }

    /// <summary>"没张开"时的缩放（常驻模式下就是常态缩放）。</summary>
    private static float ClosedScale => AlwaysVisible ? PortalScale : PortalScale * HiddenScale;

    /// <summary>"没张开"时的染色（常驻模式下就是常态染色）。</summary>
    private static Color ClosedTint(Color rest) =>
        AlwaysVisible ? rest : new Color(rest.R, rest.G, rest.B, 0f);

    private List<(CanvasItem Layer, Color BaseTint)> Layers(bool right) => right ? _rightLayers : _leftLayers;

    private Control? Portal(bool right)
    {
        var portal = right ? _right : _left;
        return portal is not null && IsInstanceValid(portal) ? portal : null;
    }

    private void KillPortalTween(bool right)
    {
        var tween = right ? _rightTween : _leftTween;
        if (tween is not null && tween.IsValid())
        {
            tween.Kill();
        }
    }

    private void SetPortalTween(bool right, Tween tween)
    {
        if (right)
        {
            _rightTween = tween;
        }
        else
        {
            _leftTween = tween;
        }
    }

    /// <summary>
    /// 开门闪光环：拿 <c>Ray</c> 贴图做一层临时节点（门的子节点 ⇒ 跟着门走），
    /// 一边放大一边淡出，放完自己 <c>QueueFreeSafely</c>。
    /// </summary>
    private static void SpawnOpenFlash(Control portal, float seconds)
    {
        var texture = ResourceLoader.Load<Texture2D>(Const.Paths.Vfx.Ray, null, ResourceLoader.CacheMode.Reuse);
        if (texture is null)
        {
            return;
        }

        var flash = new TextureRect
        {
            Name = "OpenFlash",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = new Color(PortalTint.R, PortalTint.G, PortalTint.B, OpenFlashAlpha),
            MouseFilter = MouseFilterEnum.Ignore,
        };

        portal.AddChild(flash);
        flash.SetAnchorsPreset(LayoutPreset.FullRect);
        flash.PivotOffset = DesignSizeOf(portal) * 0.5f; // 从门的中心往外张，而不是从左上角

        var duration = seconds * 1.7f;
        var tween = flash.CreateTween().SetParallel();
        tween.TweenProperty(flash, "scale", Vector2.One * OpenFlashScale, duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(flash, "modulate", new Color(PortalTint.R, PortalTint.G, PortalTint.B, 0f), duration);
        tween.Chain().TweenCallback(Callable.From(() => flash.QueueFreeSafely()));
    }
}
