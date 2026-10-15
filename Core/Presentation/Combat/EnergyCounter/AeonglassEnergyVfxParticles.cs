using System.Reflection;

using Godot;
using Godot.Collections;

using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace STS2_Aeonglass.Core.Presentation.Combat.EnergyCounter;
/// <summary>能量计数器的<b>粒子特效容器</b>（场景子节点脚本）。</summary>
/// <remarks>
/// ① 职责：承载能量变化时的粒子播放。
/// ② 调用时机：计数器实例化 / 能量变化事件。
/// ③ 依赖：粒子场景 + 反射写的 <c>ParticlesField</c>。
/// ④ ⚠️ 粒子资源缺失只丢特效、<b>不影响计数</b>；反射拿不到字段会跳过（日志会写）。
/// </remarks>

public partial class AeonglassEnergyVfxParticles : NParticlesContainer
{
    private static readonly FieldInfo ParticlesField =
        typeof(NParticlesContainer).GetField("_particles", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("NParticlesContainer._particles not found");

    public override void _Ready()
    {
        if (ParticlesField.GetValue(this) is not Array<GpuParticles2D> { Count: > 0 })
        {
            var collected = new Array<GpuParticles2D>();
            foreach (var child in GetChildren())
            {
                if (child is GpuParticles2D gpu)
                {
                    collected.Add(gpu);
                }
            }

            ParticlesField.SetValue(this, collected);
        }

        base._Ready();
    }
}
