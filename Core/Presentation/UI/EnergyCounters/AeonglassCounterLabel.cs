using MegaCrit.Sts2.addons.mega_text;

namespace STS2_Aeonglass.Core.Presentation.UI.EnergyCounters;

/// <summary>能量数字的自动缩放标签（<see cref="MegaLabel" /> 的 mod 内包装）。</summary>
/// <remarks>
/// 本体计数器的 <c>Label</c> 节点挂的是游戏内的 <c>res://addons/mega_text/MegaLabel.cs</c>，
/// mod 的 Godot 工程同样加载不到 —— 所以包一层，场景里挂我们的类，行为完全继承。
/// </remarks>
public partial class AeonglassCounterLabel : MegaLabel;
