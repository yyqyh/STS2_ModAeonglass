using Godot;

using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;

/// <summary>
/// 意图卡 ghost 的移动动画：用本体的 <see cref="NCardTrailVfx" /> 做粒子轨迹，
/// 轨迹参数走与 <c>NCardFlyVfx</c> 同形状的贝塞尔曲线。
/// </summary>
internal sealed partial class IntentCardMotionVfx : Node
{
    private AeonglassIntentCardView _ghost = null!;
    private NCardTrailVfx? _trail;
    private Vector2 _start;
    private Vector2 _end;
    private float _duration;
    private float _delay;
    private float _elapsed;
    private float _arc;
    private Action? _onDone;

    public static void Play(
        Control overlay,
        AeonglassIntentCardView ghost,
        Vector2 start,
        Vector2 end,
        float duration,
        float delay = 0f,
        Action? onDone = null)
    {
        if (TestMode.IsOn)
        {
            ghost.Wrapper.QueueFreeSafely();
            onDone?.Invoke();
            return;
        }

        var mover = new IntentCardMotionVfx
        {
            _ghost = ghost,
            _start = start,
            _end = end,
            _duration = Math.Max(0.05f, duration),
            _delay = Math.Max(0f, delay),
            _arc = Mathf.Clamp(start.DistanceTo(end) * 0.18f, 36f, 140f),
            _onDone = onDone,
        };

        ghost.Wrapper.Visible = true;
        overlay.AddChildSafely(mover);
        mover.PlaceAt(start);
        mover._trail = NCardTrailVfx.Create(ghost.Card, Const.Paths.BaseGame.CardTrailIronclad);
        if (mover._trail is not null)
        {
            overlay.AddChildSafely(mover._trail);
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_delay > 0f)
        {
            _delay -= dt;
            return;
        }

        _elapsed += dt;
        var t = Mathf.Clamp(_elapsed / _duration, 0f, 1f);
        var control = _start + (_end - _start) * 0.5f;
        control.Y -= _arc;
        var point = MathHelper.BezierCurve(_start, _end, control, t);
        PlaceAt(point);

        // 末段淡出；轨迹自己按本体节奏收尾。
        _ghost.Wrapper.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0.72f, 1f, t));

        if (t >= 1f)
        {
            _trail?.QueueFreeSafely();
            _ghost.Wrapper.QueueFreeSafely();
            _onDone?.Invoke();
            QueueFree();
        }
    }

    private void PlaceAt(Vector2 center)
    {
        _ghost.Wrapper.GlobalPosition = center - _ghost.SlotSize * 0.5f;
    }
}
