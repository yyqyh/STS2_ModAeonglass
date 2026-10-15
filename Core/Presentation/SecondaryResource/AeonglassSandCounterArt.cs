using Godot;

namespace STS2_Aeonglass.Core.Presentation.SecondaryResource;
/// <summary>沙砾计数器里的<b>5 层沙漏艺术</b>（<c>aeonglass_sand_counter_art.tscn</c> 的根脚本）。</summary>
/// <remarks>
/// ① 职责：按读数推层（<c>PushLevel</c>）—— 逐层显示沙漏的填充 / 发光。
/// ② 调用时机：计数器实例化 + 每次沙砾读数变化。
/// ③ 依赖：5 张分层图（<c>Const.Paths.Ui.*</c>）；层序 = 场景子节点序。
/// ④ ⚠️ 图是透明空槽，出图后同名覆盖；<b>层序别改</b>，否则推层看起来会乱。
/// </remarks>

public partial class AeonglassSandCounterArt : Control
{
    // ================== 摇摆 ==================

    /// <summary>摇摆幅度（度）。</summary>
    private const float SwayDegrees = 7f;

    /// <summary>摇摆周期（秒）—— 与能量球那种"慢呼吸"一个量级。</summary>
    private const float SwayPeriodSeconds = 3.5f;

    // ================== 180° 翻转 ==================

    /// <summary>翻转时长（秒）。</summary>
    private const float FlipSeconds = 0.7f;

    /// <summary>过冲比例（8%）。</summary>
    private const float FlipOvershootRatio = 0.08f;

    /// <summary>翻转的前 60% 用来"把上腔沙倒空"，剩下 40% 抬到新值。</summary>
    private const float FlipDrainPortion = 0.6f;

    // ================== 流沙 ==================

    /// <summary>
    /// 流沙速度（本控件 64 空间下的 px/s）：文档给的 40 是按 128 画布算的。
    /// 这里 64 空间 ←先减半→ 20，再由 <c>Layers.scale = 1.5</c> 放大 ⇒ 屏幕上约 30 px/s
    /// （与"按 96 控制等比：40 × 96/128 = 30"一致）。
    /// </summary>
    private const float StreamPixelsPerSecond = 20f;

    /// <summary>贴图里那条沙的纵向周期（64 画布坐标）—— 也就是两张拼接的偏移量，取模用它。</summary>
    private const float StreamPeriod = 9.75f;

    private static readonly Vector2 StreamABase = new(-26.5f, -25.75f);
    private static readonly Vector2 StreamBBase = new(-26.5f, -35.5f);

    // ================== 宝珠层反馈 ==================

    /// <summary>蓝光晕闪白的时长（秒）。</summary>
    private const float GlowFlashSeconds = 0.25f;

    /// <summary>倒置被拒时宝珠变红的时长（秒）—— 用户指定 1 秒。</summary>
    private const float OrbRedSeconds = 1.0f;

    /// <summary>
    /// 闪白的 modulate：光晕均色约 (141,201,176)，乘这个正好推到 (255,255,255)。
    /// <b>必须大于 1</b> —— multiply 只能压暗，压不出白。
    /// </summary>
    private static readonly Color GlowFlashModulate = new(1.81f, 1.27f, 1.45f);

    /// <summary>变红的 modulate（交接文档给的值）。</summary>
    private static readonly Color OrbRedModulate = new(1.0f, 0.40f, 0.38f);

    // ================== 贴图缓存 ==================

    private static readonly Texture2D?[] TopTextures = new Texture2D?[Const.Paths.Ui.SandCounter.MaxLevel + 1];
    private static readonly Texture2D?[] BottomTextures = new Texture2D?[Const.Paths.Ui.SandCounter.MaxLevel + 1];

    // ================== 节点 ==================

    private Control _layers = null!;
    private TextureRect _top = null!;
    private TextureRect _bottom = null!;
    private Control _streamClip = null!;
    private TextureRect _streamA = null!;
    private TextureRect _streamB = null!;
    private TextureRect _orb = null!;

    /// <summary>
    /// <b>受重力影响的图层</b>：上/下腔沙 + 两层流沙。倒置时这几层要各自 counter-rotate 180°，
    /// 让沙面/沙堆/流沙始终朝向屏幕下方（跟 <c>Layers</c> 一起转的话它们会"朝天"）。
    /// </summary>
    private Control[] _gravityLayers = [];

    // ================== 状态 ==================

    private int _level = -1;
    private float _time;
    private float _streamOffset;

    /// <summary>摇摆的基准角；每翻转一次 +180°（倒过来的沙漏就一直是倒的，不会"弹回去"）。</summary>
    private float _baseAngle;

    /// <summary>当前是不是"倒着"的（<c>_baseAngle</c> 为 180° 的奇数倍）—— 决定上下腔贴图要不要对调。</summary>
    private bool _upsideDown;

    private bool _flipping;
    private float _flipElapsed;
    private bool _flipSwapped;
    private int _flipFrom;
    private int _flipTarget;

    private float _orbRedRemaining;
    private Tween? _glowTween;

    public override void _Ready()
    {
        _layers = GetNode<Control>("Layers");
        _top = GetNode<TextureRect>("Layers/Top");
        _bottom = GetNode<TextureRect>("Layers/Bottom");
        _streamClip = GetNode<Control>("Layers/StreamClip");
        _streamA = GetNode<TextureRect>("Layers/StreamClip/StreamA");
        _streamB = GetNode<TextureRect>("Layers/StreamClip/StreamB");
        _orb = GetNode<TextureRect>("Layers/Orb");

        // 摇摆绕中心转：不设的话默认 (0,0)，会绕左上角甩。
        _layers.PivotOffset = new Vector2(32f, 32f);

        // 重力对齐的图层各自绕自己中心翻（它们都是 64×64 的整画布）。
        _gravityLayers = [_top, _bottom, _streamA, _streamB];
        foreach (var layer in _gravityLayers)
        {
            layer.PivotOffset = new Vector2(32f, 32f);
        }

        ApplyGravityFacing();
        _streamClip.Visible = false;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;

        if (_flipping)
        {
            // 翻转期间不让摇摆改 rotation（tween 在用那条属性）。
            ProcessFlip(dt);
        }
        else
        {
            _layers.RotationDegrees =
                _baseAngle + SwayDegrees * Mathf.Sin(_time * Mathf.Tau / SwayPeriodSeconds);
        }

        ProcessStream(dt);
        ProcessOrbRed(dt);
    }

    /// <summary>按当前沙砾值换档（上腔 = level、下腔 = 6 − level）。翻转期间只记账，不打断演出。</summary>
    public void SetLevel(int level)
    {
        level = Const.Paths.Ui.SandCounter.ClampLevel(level);

        if (_flipping)
        {
            _flipTarget = level;
            return;
        }

        ApplyLevel(level);
    }

    /// <summary>播 180° 翻转（倒置成功后调）。<paramref name="newLevel" /> = 交换后的新沙砾值。</summary>
    public void PlayInvertFlip(int newLevel)
    {
        _flipTarget = Const.Paths.Ui.SandCounter.ClampLevel(newLevel);
        _flipFrom = _level < 0 ? _flipTarget : _level;
        _flipElapsed = 0f;
        _flipSwapped = false;
        _flipping = true;

        var to = _baseAngle + 180f;
        _baseAngle = to;

        var tween = CreateTween();
        tween.TweenProperty(_layers, "rotation_degrees", to + 180f * FlipOvershootRatio, FlipSeconds * 0.72f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(_layers, "rotation_degrees", to, FlipSeconds * 0.28f)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>沙砾值变化 → 蓝色光晕闪白一次（红珠期间跳过，避免两个反馈抢同一层）。</summary>
    public void FlashGlowOnce()
    {
        if (_orbRedRemaining > 0f)
        {
            return;
        }

        _glowTween?.Kill();
        _glowTween = CreateTween();
        _glowTween.TweenProperty(_orb, "modulate", GlowFlashModulate, GlowFlashSeconds * 0.4f);
        _glowTween.TweenProperty(_orb, "modulate", Colors.White, GlowFlashSeconds * 0.6f);
    }

    /// <summary>倒置被拒（能量不足）→ 宝珠与光晕变红 1 秒。</summary>
    public void FlashOrbRed()
    {
        _glowTween?.Kill();
        _orbRedRemaining = OrbRedSeconds;
        _orb.Modulate = OrbRedModulate;
    }

    /// <param name="level">要显示的沙砾值（0..6）。</param>
    /// <param name="force">即使档位没变也重刷（倒置把上下映射翻过来时必须强制重刷）。</param>
    private void ApplyLevel(int level, bool force = false)
    {
        level = Const.Paths.Ui.SandCounter.ClampLevel(level);
        if (!force && level == _level)
        {
            return;
        }

        _level = level;

        // 视觉上腔永远显示"沙砾值"：正着时上腔是 Top 图层，倒过来后上腔变成 Bottom 图层。
        var levelTexture = TopTexture(level);
        var restTexture = BottomTexture(level);
        _top.Texture = _upsideDown ? restTexture : levelTexture;
        _bottom.Texture = _upsideDown ? levelTexture : restTexture;

        // 档 0（沙全落到底）与档 6（沙全在上腔）都没有沙在流。
        _streamClip.Visible = level > 0 && level < Const.Paths.Ui.SandCounter.MaxLevel;
    }

    /// <summary>
    /// 按重力摆正"沙 / 流沙"图层：容器倒着（<c>Layers</c> = 180°）时，这几层各自再转 180°，
    /// 于是它们的艺术在屏幕上永远是正的（沙坐底、流沙往下）。
    /// </summary>
    private void ApplyGravityFacing()
    {
        var facing = _upsideDown ? 180f : 0f;
        foreach (var layer in _gravityLayers)
        {
            layer.RotationDegrees = facing;
        }
    }

    private void ProcessFlip(float dt)
    {
        _flipElapsed += dt;
        var portion = _flipElapsed / FlipSeconds;

        if (portion >= 1f)
        {
            _flipping = false;
            ApplyLevel(_flipTarget);
            return;
        }

        if (portion < FlipDrainPortion)
        {
            // 前半段：上腔沙一路倒空（下腔同步变多 —— 同一个 level 保证上 + 下 = 6）。
            var k = 1f - portion / FlipDrainPortion;
            ApplyLevel(Mathf.RoundToInt(_flipFrom * k));
            return;
        }

        // 转过一半（视觉上下已经对调）时把两层的映射翻过来，强制重刷一次。
        if (!_flipSwapped)
        {
            _flipSwapped = true;
            _upsideDown = !_upsideDown;
            ApplyGravityFacing();
            ApplyLevel(_level < 0 ? 0 : _level, force: true);
        }

        // 落定之后再把沙抬到新值。
        var fill = (portion - FlipDrainPortion) / (1f - FlipDrainPortion);
        ApplyLevel(Mathf.RoundToInt(_flipTarget * fill));
    }

    private void ProcessStream(float dt)
    {
        if (!_streamClip.Visible)
        {
            return;
        }

        _streamOffset = (_streamOffset + StreamPixelsPerSecond * dt) % StreamPeriod;
        var shift = new Vector2(0f, _streamOffset);
        _streamA.Position = StreamABase + shift;
        _streamB.Position = StreamBBase + shift;
    }

    private void ProcessOrbRed(float dt)
    {
        if (_orbRedRemaining <= 0f)
        {
            return;
        }

        _orbRedRemaining -= dt;
        if (_orbRedRemaining <= 0f)
        {
            _orbRedRemaining = 0f;
            _orb.Modulate = Colors.White;
        }
    }

    private static Texture2D? TopTexture(int level)
    {
        level = Const.Paths.Ui.SandCounter.ClampLevel(level);
        return TopTextures[level] ??= ResourceLoader.Load<Texture2D>(Const.Paths.Ui.SandCounter.Top(level));
    }

    private static Texture2D? BottomTexture(int level)
    {
        level = Const.Paths.Ui.SandCounter.ClampLevel(level);
        return BottomTextures[level] ??= ResourceLoader.Load<Texture2D>(Const.Paths.Ui.SandCounter.Bottom(level));
    }
}
