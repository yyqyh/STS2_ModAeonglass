using Godot;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// M2「坍缩 / 唤回」战斗按键的<b>月牙艺术</b>（挂在
/// <c>scenes/ui/gap/aeonglass_collapse_button.tscn</c> 上）。
/// </summary>
/// <remarks>
/// <para>
/// 三段表现（交接文档 §二）：
/// </para>
/// <list type="bullet">
///   <item><description><b>常态 A</b>：裂缝<b>偶尔冒一下玫红</b>（偶尔、短促，不是常亮）＋ 内层<b>绕圆 A 的圆心</b>轻摆；</description></item>
///   <item><description><b>变化</b>（触发一次）：裂缝<b>闪一次白光</b> → 内层<b>从上至下</b>逐渐变成玫红；</description></item>
///   <item><description><b>常态 B</b>：内层维持玫红，并<b>继续摆动</b>。</description></item>
/// </list>
/// <para>
/// <b>裂缝为什么不用 modulate</b>：那张素材是近黑（均色约 <c>(34,30,22)</c>），multiply 只会更黑，
/// 根本提不出玫红/白。所以场景给它挂了 <c>mix(原色 → tint, amount)</c> 的两行 shader，
/// 闪色靠 <c>tint</c> / <c>amount</c> 两个 uniform（和美术脚本自己的做法一致）。
/// </para>
/// <para>
/// <b>摆动的支点</b>是圆 A 的圆心（256 画布 <c>(128,134)</c> → 128 画布 <c>(64,67)</c>），
/// <b>不是</b>内层自己的几何中心 —— 这是交接文档特别点名的。
/// </para>
/// </remarks>
public partial class CollapseButtonArtUi : Control
{
    /// <summary>设计尺寸（画布 128，贴图 256 缩一半显示）。</summary>
    private const float DesignSize = 128f;

    /// <summary>圆 A 的圆心（128 画布）。</summary>
    private static readonly Vector2 CircleACenter = new(64f, 67f);

    // ================== 常态摇摆 ==================

    /// <summary>摆幅（度）—— 用户 2026-10-13 看过实机后要求"加大"（文档原口径是 ±4° 以内）。想再调就改这一个数。</summary>
    private const float SwayDegrees = 7f;

    /// <summary>摆动周期（秒）。</summary>
    private const float SwayPeriodSeconds = 2.6f;

    // ================== 常态 A：裂缝偶冒玫红 ==================

    private const float IdleGapMin = 1.2f;
    private const float IdleGapMax = 3.0f;
    private const float IdleRise = 0.08f;
    private const float IdleHold = 0.10f;
    private const float IdleFall = 0.26f;

    // ================== 变化：闪白 + 上→下擦拭 ==================

    private const float ChangeRise = 0.06f;
    private const float ChangeHold = 0.08f;
    private const float ChangeFall = 0.34f;

    /// <summary>玫红层从上揭到底要多久。</summary>
    private const float WipeSeconds = 0.32f;

    private static readonly Color RoseTint = new(1.0f, 0.30f, 0.62f);
    private static readonly Color WhiteTint = new(1f, 1f, 1f);

    private Control _sway = null!;
    private Control _roseClip = null!;
    private ShaderMaterial _crackMaterial = null!;

    /// <summary>是否处于常态 B（内层玫红、不再冒玫红闪）。</summary>
    private bool _rose;

    private float _time;
    private float _idleTimer;

    /// <summary>玫红层当前揭出的高度比例（0 = 全原色，1 = 全玫红）。</summary>
    private float _wipe;
    private float _wipeTarget;

    /// <summary>裂缝闪色的相位（&lt;0 = 空闲）；用三段时长直接算 amount，避免依赖 tween API。</summary>
    private float _flashElapsed = -1f;
    private float _flashRise;
    private float _flashHold;
    private float _flashFall;

    public override void _Ready()
    {
        _sway = GetNode<Control>("InnerSway");
        _roseClip = GetNode<Control>("InnerSway/RoseClip");
        _crackMaterial = (ShaderMaterial)GetNode<TextureRect>("Crack").Material;

        _sway.PivotOffset = CircleACenter;

        SetCrackAmount(0f);
        _wipe = 0f;
        _wipeTarget = 0f;
        ApplyWipe();

        _idleTimer = NextIdleGap();
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _time += dt;

        // 常态摆动：绕圆 A 的圆心（两种常态都在摆）。
        _sway.RotationDegrees = SwayDegrees * Mathf.Sin(_time * Mathf.Tau / SwayPeriodSeconds);

        // 常态 A 才偶尔冒玫红；常态 B 已经整块玫红，不再闪。
        if (!_rose)
        {
            _idleTimer -= dt;
            if (_idleTimer <= 0f)
            {
                _idleTimer = NextIdleGap();
                FlashCrack(RoseTint, IdleRise, IdleHold, IdleFall);
            }
        }

        ProcessCrackFlash(dt);

        if (!Mathf.IsEqualApprox(_wipe, _wipeTarget))
        {
            var step = dt / WipeSeconds;
            _wipe = Mathf.MoveToward(_wipe, _wipeTarget, step);
            ApplyWipe();
        }
    }

    /// <summary>
    /// 切常态：<paramref name="rose" /> = true → 常态 B（内层玫红）。
    /// </summary>
    /// <param name="rose">目标常态。</param>
    /// <param name="playChange">
    /// 是否播"变化"演出（裂缝闪一次白 + 内层<b>带方向</b>地擦）。
    /// <b>两个方向都演</b>：进常态 B = 擦拭边从上往下揭出玫红；退常态 A = 同一条边从下往上退回原色（反向动画）。
    /// 初始状态用 <c>false</c>（直接落位，不演）。
    /// </param>
    public void SetRose(bool rose, bool playChange = false)
    {
        var changed = rose != _rose;
        _rose = rose;

        if (playChange && changed)
        {
            // 从"当前揭出量"平滑推向目标 —— 两个方向共用同一套，不重置就不会有跳变。
            FlashCrack(WhiteTint, ChangeRise, ChangeHold, ChangeFall);
            _wipeTarget = rose ? 1f : 0f;
            return;
        }

        // 不演：直接落到位（玫红层要么全揭、要么全收）。
        _wipe = rose ? 1f : 0f;
        _wipeTarget = _wipe;
        ApplyWipe();
    }

    /// <summary>裂缝闪一次色（<paramref name="tint" /> 是目标色，<paramref name="amount" /> 0→1→0）。</summary>
    private void FlashCrack(Color tint, float rise, float hold, float fall)
    {
        _crackMaterial.SetShaderParameter("tint", tint);
        _flashRise = rise;
        _flashHold = hold;
        _flashFall = fall;
        _flashElapsed = 0f;
    }

    private void ProcessCrackFlash(float dt)
    {
        if (_flashElapsed < 0f)
        {
            return;
        }

        _flashElapsed += dt;
        var total = _flashRise + _flashHold + _flashFall;
        float amount;

        if (_flashElapsed < _flashRise)
        {
            amount = _flashElapsed / _flashRise;
        }
        else if (_flashElapsed < _flashRise + _flashHold)
        {
            amount = 1f;
        }
        else
        {
            amount = 1f - (_flashElapsed - _flashRise - _flashHold) / _flashFall;
        }

        SetCrackAmount(Mathf.Clamp(amount, 0f, 1f));

        if (_flashElapsed >= total)
        {
            SetCrackAmount(0f);
            _flashElapsed = -1f;
        }
    }

    private void SetCrackAmount(float amount)
    {
        _crackMaterial.SetShaderParameter("amount", amount);
    }

    /// <summary>把"揭出高度"落到节点上：RoseClip 在顶部、高度 = 比例 × 设计尺寸 ⇒ 从上往下揭。</summary>
    private void ApplyWipe()
    {
        _roseClip.Position = Vector2.Zero;
        _roseClip.Size = new Vector2(DesignSize, DesignSize * _wipe);
    }

    private static float NextIdleGap() => (float)GD.RandRange(IdleGapMin, IdleGapMax);
}
