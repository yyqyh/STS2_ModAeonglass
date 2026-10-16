using Godot;

namespace STS2_Aeonglass.Core.Presentation.Support;

/// <summary>
/// 粒子特效的**共用件**：软圆点贴图 / 颜色渐变 / "速度连续"三件套。
/// </summary>
/// <remarks>
/// ① 职责：收掉"发牌扫描带"与"传送门漩涡"各自重复的那几行（贴图、ramp、连续速度设置）。
/// ② 调用时机：建粒子系统时。
/// ③ 依赖：Godot 的 <c>GpuParticles2D</c> / <c>GradientTexture1D</c>。
/// ④ ⚠️ <see cref="Continuous" /> 那三项**必须**一起设：`FixedFps` 默认 30（30Hz 步进 ⇒ 一顿一顿）、
///    非 0 `Explosiveness`（发射成串 ⇒ 跳）。踩过一次，别再各自抄。
/// </remarks>

internal static class ParticleFx
{
    private static Texture2D? _softDot;
    private static readonly Dictionary<(Color From, Color To), GradientTexture1D> RampCache = [];

    /// <summary>16×16 径向柔化圆点（代码生成、不依赖美术），进程内静态缓存。</summary>
    internal static Texture2D SoftDot()
    {
        if (_softDot is { } cached && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }

        const int size = 16;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var center = (size - 1) * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Mathf.Sqrt(((x - center) * (x - center)) + ((y - center) * (y - center))) / center;
                var alpha = Mathf.Clamp(1f - distance, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 0.92f, 0.75f, alpha * alpha));
            }
        }

        _softDot = ImageTexture.CreateFromImage(image);
        return _softDot;
    }

    /// <summary>两色渐变（按"首色 + 末色"静态缓存，粒子 <c>ColorRamp</c> 用）。</summary>
    internal static GradientTexture1D Ramp(Color from, Color to)
    {
        if (RampCache.TryGetValue((from, to), out var cached) && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }

        var ramp = new GradientTexture1D
        {
            Gradient = new Gradient
            {
                Colors = [from, to],
                Offsets = [0f, 1f],
            },
        };

        RampCache[(from, to)] = ramp;
        return ramp;
    }

    /// <summary>
    /// 把粒子系统调成"整体速度连续"：跟随渲染帧步进 + 帧间插值 + 连续发射（不出脉冲）。
    /// </summary>
    internal static void Continuous(GpuParticles2D particles)
    {
        particles.FixedFps = 0;
        particles.Interpolate = true;
        particles.Explosiveness = 0f;
    }
}
