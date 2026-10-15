using MegaCrit.Sts2.addons.mega_text;

namespace STS2_Aeonglass.Core.Presentation.Combat.EnergyCounter;
/// <summary>能量数字 Label 的顶点脚本（能量计数器场景的子节点）。</summary>
/// <remarks>
/// ① 职责：数字 Label 的实例类型（描边色由角色类 <c>EnergyLabelOutlineColor</c> 给）。
/// ② 调用时机：能量计数器场景实例化时。
/// ③ 依赖：<c>aeonglass_energy_counter.tscn</c> 的子节点挂载。
/// ④ ⚠️ 只有几行：Godot <b>一脚本一文件</b>，不要为了"看起来简洁"合并进计数器脚本。
/// </remarks>

public partial class AeonglassCounterLabel : MegaLabel;
