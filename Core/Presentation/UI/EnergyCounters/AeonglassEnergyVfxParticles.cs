using System.Reflection;

using Godot;
using Godot.Collections;

using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace STS2_Aeonglass.Core.Presentation.UI.EnergyCounters;

/// <summary>能量喷发粒子容器（<see cref="NParticlesContainer" /> 的 mod 内子类）。</summary>
/// <remarks>
/// <para>
/// 本体计数器要求 <c>%EnergyVfxBack</c> / <c>%EnergyVfxFront</c> 是 <see cref="NParticlesContainer" />；
/// 但它的粒子数组是基类的**私有字段**，跨程序集把场景序列化/反序列化会失败 ——
/// 所以这里在 <c>_Ready</c> 里从子节点收集 <see cref="GpuParticles2D" /> 写回那个字段。
/// </para>
/// <para>做法照小丑的 <c>JesterEnergyVfxParticles</c>（同一套坑，已在小丑上验证过）。</para>
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
