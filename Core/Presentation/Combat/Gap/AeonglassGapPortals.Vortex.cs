using Godot;

using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

using STS2_Aeonglass.Core.Presentation.Art;
using STS2_Aeonglass.Core.Presentation.Support;

namespace STS2_Aeonglass.Core.Presentation.Combat.Gap;

/// <summary>传送门的<b>青色漩涡</b>：开 / 关都先出漩涡（<c>AeonglassGapPortals</c> 的 partial）。</summary>
/// <remarks>
/// ① 职责：造漩涡（环状发射 + 绕门心转 + 吸/吐径向加速）+ 淡入淡出。
/// ② 调用时机：<c>PlayOpenAsync</c> / <c>PlayCloseAsync</c>。
/// ③ 依赖：<c>ParticleFx</c> / <c>AeonglassUiPalette</c> / <c>Const.Paths.Vfx</c>。
/// ④ ⚠️ 时长/粒子数/半径/径向加速度等常量在主页面的"青色漩涡"块里（同一个 partial 类）。
/// </remarks>

public partial class AeonglassGapPortals
{

    // ==================== 青色漩涡 ====================

    /// <summary>
    /// 造一个青色漩涡挂在**门自己身上**（跟着门走、跟着门一起缩放），位置在门的中心。
    /// </summary>
    /// <remarks>
    /// 做法：`GpuParticles2D` + 环状发射 + `OrbitVelocity`（绕门心转）+ 径向加速度（吸进 / 吐出）。
    /// 材质按"吸/吐 + 环半径"**静态缓存**（每次开合不再重新构造）；"速度连续"三件套走 <c>ParticleFx.Continuous</c>。
    /// </remarks>
    private GpuParticles2D? SpawnVortex(Control portal, bool suckIn)
    {
        try
        {
            var design = DesignSizeOf(portal);
            var radius = Mathf.Max(8f, Mathf.Min(design.X, design.Y) * 0.5f * VortexRadiusFactor);
            var vortex = new GpuParticles2D
            {
                Name = "GapVortex",
                Amount = VortexAmount,
                Lifetime = VortexLifeSeconds,
                LocalCoords = false,
                Emitting = true,
                Texture = VortexTexture(),
                ProcessMaterial = VortexMaterial(suckIn, radius),
                Position = design * 0.5f,                       // 门的视觉中心（门以自身中心为锚点缩放）
                Modulate = new Color(1f, 1f, 1f, 0f),
                ZIndex = 1,                                     // 画在门那几层之上（旋涡"包住"门）
            };

            // "速度连续"三件套（FixedFps = 0 + Interpolate + Explosiveness = 0）。
            ParticleFx.Continuous(vortex);
            portal.AddChildSafely(vortex);
            return vortex;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Gap} 漩涡创建失败（只影响观感，门照常开合）：{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>把漩涡粒子系统的材质缓存起来（key = 吸/吐 + 环半径取整）。</summary>
    private static ParticleProcessMaterial VortexMaterial(bool suckIn, float radius)
    {
        var key = (SuckIn: suckIn, Radius: Mathf.RoundToInt(radius));
        if (VortexMaterials.TryGetValue(key, out var cached) && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }

        var radial = suckIn ? -VortexRadialAccel : VortexRadialAccel;
        var material = new ParticleProcessMaterial
        {
            OrbitVelocityMin = -VortexOrbitMax,
            OrbitVelocityMax = -VortexOrbitMin,
            RadialAccelMin = radial,
            RadialAccelMax = radial,
            Gravity = Vector3.Zero,
            DampingMin = 0f,
            DampingMax = 0f,
            ScaleMin = VortexScaleMin,
            ScaleMax = VortexScaleMax,
            AngularVelocityMin = -70f,
            AngularVelocityMax = 70f,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingRadius = radius,
            EmissionRingInnerRadius = radius * 0.55f,
            EmissionRingAxis = new Vector3(0f, 0f, 1f),
            // 尾端透明（首端 = 沙漏青）
            ColorRamp = ParticleFx.Ramp(AeonglassUiPalette.Cyan, new Color(AeonglassUiPalette.Cyan.R, AeonglassUiPalette.Cyan.G, AeonglassUiPalette.Cyan.B, 0f)),
        };

        VortexMaterials[key] = material;
        return material;
    }

    /// <summary>漩涡粒子贴图（复用 vfx 资源；引擎侧 Reuse + 这里再兜一层缓存）。</summary>
    private static Texture2D? VortexTexture()
    {
        if (_vortexTexture is { } cached && GodotObject.IsInstanceValid(cached))
        {
            return cached;
        }

        _vortexTexture = ResourceLoader.Load<Texture2D>(VortexTexturePath, null, ResourceLoader.CacheMode.Reuse);
        return _vortexTexture;
    }

    /// <summary>漩涡淡入（0 → 1）。</summary>
    private async Task FadeVortexInAsync(GpuParticles2D? vortex)
    {
        if (vortex is null || !IsInstanceValid(vortex))
        {
            return;
        }

        var tween = vortex.CreateTween();
        tween.TweenProperty(vortex, "modulate:a", 1f, VortexFadeInSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        await tween.AwaitFinished(this);
    }

    /// <summary>漩涡消逝（1 → 0）并释放：先停发射，剩下的粒子自然死完，再整颗撤掉。</summary>
    private async Task FadeVortexOutAsync(GpuParticles2D? vortex)
    {
        if (vortex is null || !IsInstanceValid(vortex))
        {
            return;
        }

        vortex.Emitting = false;

        var tween = vortex.CreateTween();
        tween.TweenProperty(vortex, "modulate:a", 0f, VortexFadeOutSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        await tween.AwaitFinished(this);

        if (IsInstanceValid(vortex))
        {
            vortex.QueueFreeSafely();
        }
    }
}

