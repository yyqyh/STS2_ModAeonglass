using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Presentation.Support;

using STS2_Aeonglass.Core.Systems.Gap;
namespace STS2_Aeonglass.Core.Presentation.Combat.Gap;
/// <summary>间隙的<b>静态传送门</b>：左右各一道，贴在「间隙」显示牌堆容器的两端。</summary>
/// <remarks>
/// ① 职责：按容器位置摆门、播放开门/关门与粒子过场。
/// ② 调用时机：战斗房间挂载时建；<b>只有动画期间显示</b>。
/// ③ 依赖：间隙额外手牌容器（<c>NodeSearch</c> 找 <c>NModExtraHand</c>）+ 传送门场景（<c>Const.Paths</c>）。
/// ④ ⚠️ 容器找不到 ⇒ 整段演出跳过（机制照常）。坐标：本节点在<b>场景空间</b>（挂 <c>%CombatVfxContainer</c>）、
/// 额外手牌容器在<b>屏幕空间</b> ⇒ <c>SyncPosition</c> 每轮把自己按 <c>1/相机缩放</c> 抵消并把容器全局坐标换算到局部，
/// 内部一律按"屏幕像素"算；改容器落点不用重新对表。
/// </remarks>

public partial class AeonglassGapPortals : Control
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

    /// <summary>
    /// 是否抵消父级（场景空间）的相机缩放。
    /// </summary>
    /// <remarks>
    /// <c>true</c>（默认）= 门的**屏幕尺寸与偏移量维持出图时的设计值**，只有层级/震动随场景（用户 2026-10-15 口径）；
    /// 想让门跟着世界一起缩放（受伤/镜头缩放时一起变小）就改成 <c>false</c>。
    /// </remarks>
    private const bool CompensateCameraScale = true;

    /// <summary>
    /// 传送门挂在哪个"场景层"里（2026-10-15 用户口径：门属于世界，不在 UI 里）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CombatVfx</c>（默认，用户指定）= <c>%CombatVfxContainer</c>（本体给它设了 <c>ZIndex = -9</c>）。
    /// <c>BackCombatVfx</c> = <c>%BackCombatVfxContainer</c>（z 相对 0 ⇒ 与人物同层、按树序排在人物之前）。
    /// </para>
    /// <para>
    /// ⚠️ 本体没关掉 <c>z_as_relative</c>，所以 z 是**沿父链累加**的：<c>CombatSceneContainer</c> 是 -10 ⇒
    /// <c>CombatVfxContainer</c> 的有效 z ≈ <b>-19</b>（比背景、人物那些 -10 的兄弟还低），
    /// <c>BackCombatVfxContainer</c> 则是 -10（与人物同层、树序在前）。**进战斗时 log 里会打一行实测值**
    /// （`[间隙] 场景分层…`），门被背景或人物盖住时照那行改这个常量即可。
    /// </para>
    /// </remarks>
    private const PortalSceneLayer SceneLayer = PortalSceneLayer.CombatVfx;

    /// <summary>传送门可选的场景层（见 <see cref="SceneLayer" />）。</summary>
    private enum PortalSceneLayer
    {
        /// <summary><c>%CombatVfxContainer</c>（本体的"战斗特效"容器，z 相对 -9）。</summary>
        CombatVfx,

        /// <summary><c>%BackCombatVfxContainer</c>（背景层特效容器，z 相对 0）。</summary>
        BackCombatVfx,
    }

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
    internal static AeonglassGapPortals? Instance { get; private set; }

    /// <summary>挂进战斗 UI（由 <c>CombatUiMountPatch</c> 调）。</summary>
    internal static void Attach(Node room)
    {
        AeonglassSettingStore.Initialize();

        var setting = AeonglassSettingStore.Current;
        if (!setting.GapEnabled)
        {
            return;
        }

        if (room is not MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom combatRoom)
        {
            return;
        }

        // 2026-10-15 用户口径：挂到**战斗特效容器**（`%CombatVfxContainer`）—— 门属于"世界"而不是 UI：
        // 会被人物节点遮住、跟着屏幕震动一起晃。⚠️ 那个容器在 `%CombatSceneContainer` 里，
        // **继承相机缩放** ⇒ 本节点每次定位都把自己的 Scale 抵消成 1/缩放，并把额外手牌容器的全局坐标
        // 换算到本节点局部（内部一切仍是"屏幕像素"口径）。取不到容器就退回挂 UI（旧行为，能跑）。
        var scene = combatRoom.SceneContainer;
        var vfx = SceneLayer == PortalSceneLayer.BackCombatVfx
            ? scene?.GetNodeOrNull<Control>("%BackCombatVfxContainer")
            : combatRoom.CombatVfxContainer;

        if (vfx is null || !GodotObject.IsInstanceValid(vfx) || combatRoom.Ui is null)
        {
            combatRoom.Ui?.AddChild(new AeonglassGapPortals { Name = nameof(AeonglassGapPortals) });
            return;
        }

        vfx.AddChild(new AeonglassGapPortals
        {
            Name = nameof(AeonglassGapPortals),
            ZIndex = -1, // 落在本体战斗特效之下、人物（其父级兄弟）之下
        });

        DumpLayerOrder(combatRoom, vfx);
    }

    /// <summary>
    /// 把"战斗场景的实际分层"打一行日志（一次性，改挂点/排查被盖住时看它）。
    /// </summary>
    /// <remarks>
    /// z 是沿父链累加的（本体没关 <c>z_as_relative</c>），所以这里按 Godot 的规则手算有效 z：
    /// 从节点往上叠 <c>ZIndex</c>，遇到 <c>ZAsRelative == false</c> 就停。
    /// </remarks>
    private static void DumpLayerOrder(MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom room, Control parent)
    {
        try
        {
            var scene = room.SceneContainer;
            var children = scene is null
                ? "(no scene)"
                : string.Join(", ", scene.GetChildren().OfType<CanvasItem>()
                    .Select(c => $"{c.Name}(z={c.ZIndex}{(c.ZAsRelative ? "rel" : "abs")})"));

            Const.Logger.Info(
                $"{Const.Prefix.Gap} 场景分层：CombatSceneContainer(z={scene?.ZIndex}rel) 子=[{children}]"
                + $"｜传送门父={parent.Name}(有效 z={EffectiveZ(parent)})"
                + $"｜人物侧 AllyContainer(有效 z={EffectiveZ(scene?.GetNodeOrNull<Control>("%AllyContainer"))})"
                + $"/ EnemyContainer(有效 z={EffectiveZ(scene?.GetNodeOrNull<Control>("%EnemyContainer"))})");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"{Const.Prefix.Gap} 场景分层打印失败（已忽略）：{ex.GetType().Name}");
        }
    }

    /// <summary>节点在 Godot 里的有效绘制序（沿父链累加 ZIndex，遇到绝对 z 就停）。</summary>
    private static int EffectiveZ(Node? node)
    {
        var z = 0;
        for (var n = node; n is CanvasItem item; n = item.GetParent())
        {
            z += item.ZIndex;
            if (!item.ZAsRelative)
            {
                break;
            }
        }

        return z;
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

        var target = GapViewPile.ZoneId;
        return NodeSearch.FindDescendant<NModExtraHand>(ui, hand => string.Equals(hand.Definition?.Id, target, StringComparison.OrdinalIgnoreCase));
    }

    private static MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom? NCombatRoomOf() =>
        MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance;

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

    /// <summary>
    /// 把自己定位到额外手牌容器的左右两端。
    /// </summary>
    /// <remarks>
    /// 坐标空间：本节点挂在 <c>%CombatVfxContainer</c>（场景空间，继承相机缩放）里，而额外手牌容器在
    /// <c>NCombatUi</c>（屏幕空间）。所以这里 ① 先把自己的 <c>Scale</c> 抵消成 <c>1/缩放</c>（内部口径还原成屏幕像素，
    /// 门的大小/描边/偏移量维持出图时的设计值），② 再把容器的全局坐标换算到本节点局部。
    /// 想让门"随世界缩放"就把 <see cref="CompensateCameraScale" /> 关掉。
    /// </remarks>
    private void SyncPosition()
    {
        if (_container is null || !IsInstanceValid(_container)
            || _left is null || !IsInstanceValid(_left))
        {
            return;
        }

        // ① 抵消父级（场景空间）的缩放：内部单位 = 屏幕像素。
        var parentScale = ResolveParentScale();
        Scale = CompensateCameraScale && parentScale > 0.001f
            ? Vector2.One / parentScale
            : Vector2.One;

        // ② 容器的全局矩形 → 本节点局部坐标（此时 1 局部单位 = 1 屏幕像素）。
        // `Control.GlobalPosition` 与 `GetGlobalTransform()` 同属"画布空间"（不含视口变换）⇒ 求逆即得本节点局部坐标。
        var handTopLeft = GetGlobalTransform().AffineInverse() * _container.GlobalPosition;
        var design = DesignSizeOf(_left);
        var centerY = handTopLeft.Y + (_container.Size.Y - design.Y) * 0.5f + VerticalOffset;

        _left.Position = new Vector2(handTopLeft.X - design.X - SideMargin, centerY);
        if (_right is not null && IsInstanceValid(_right))
        {
            _right.Position = new Vector2(handTopLeft.X + _container.Size.X + SideMargin, centerY);
        }
    }

    /// <summary>父级链累计缩放（场景空间的相机缩放）；拿不到按 1 算。</summary>
    private float ResolveParentScale() =>
        GetParent() is CanvasItem parent ? parent.GetGlobalTransform().Scale.X : 1f;

    // ==================== 给表现层（AeonglassGapSequence）用的门 API ====================

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
