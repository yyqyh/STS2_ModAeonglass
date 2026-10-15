using Godot;

using MegaCrit.Sts2.Core.Nodes.Combat;

namespace STS2_Aeonglass.Core.Presentation.Combat.EnergyCounter;
/// <summary>永世沙漏的<b>战斗能量计数器</b>（挂在 <c>scenes/combat/energy_counters/aeonglass_energy_counter.tscn</c> 上）。</summary>
/// <remarks>
/// ① 职责：能量读数与内环动画、L4 外波动。
/// ② 调用时机：本体建能量计数器时实例化（角色类 <c>AssetProfile</c> 指定）。
/// ③ 依赖：<c>Const.Paths.Ui.Energy.*</c> 的分层图 + <c>AeonglassCounterLabel</c> + 粒子节点。
/// ④ ⚠️ <b>别给 Layer4 挂 ShaderMaterial</b>（会被基类 <c>RefreshLabel</c> 抹掉）；数字描边色在角色类，不是卡池那个。
/// </remarks>

public partial class AeonglassEnergyCounter : NEnergyCounter
{
    /// <summary>L4 向外波动的最大缩放（推到最大时 alpha 已归零，所以不会露出被切掉的一圈）。</summary>
    private const float WaveScaleMax = 1.20f;

    /// <summary>L4 波动周期（秒）。</summary>
    private const float WavePeriod = 1.6f;

    /// <summary>能量为 0 时的慢速周期（秒）—— 配合基类的"没劲了"观感。</summary>
    private const float WavePeriodIdle = 4.0f;

    private Control _layersRoot = null!;
    private Control _innerRing = null!;
    private Control _layer4 = null!;
    private float _waveT;

    public override void _Ready()
    {
        base._Ready();

        _layersRoot = GetNode<Control>("%Layers");
        // 子 0 = Layer2 = 内层环（基类给它 30°/s，正好是"内环慢"）
        _innerRing = GetNode<Control>("%RotationLayers").GetChild<Control>(0);
        _layer4 = GetNode<Control>("%Layers/Layer4");

        // 绕中心缩放：tscn 里 Layer2/Layer3 已经设了 pivot，Layer4 也补上（场景被重导也不会丢）。
        if (_layer4.PivotOffset == Vector2.Zero)
        {
            _layer4.PivotOffset = Size * 0.5f;
        }
    }

    public override void _Process(double delta)
    {
        // ① 内层环改逆时针：
        //    基类把 %RotationLayers 的两个子节点**同向**加角度（子 0 = 30°/s、子 1 = 60°/s）。
        //    这里在基类转完之后，把子 0 这一帧的增量取反 ⇒ 内环逆时针、外环顺时针，两层反向。
        var before = _innerRing.RotationDegrees;
        base._Process(delta);
        _innerRing.RotationDegrees = before - (_innerRing.RotationDegrees - before);

        // ② L4 向外波动：scale 1.00 → 1.20，同时 alpha 1.00 → 0（一圈推出去并淡掉）。
        //    "能量为 0"用 %Layers.Modulate 当信号（基类此时会把它压成 DarkGray），
        //    这样不必去碰基类的 private 字段。
        var idle = _layersRoot.Modulate.R < 0.99f;
        var period = idle ? WavePeriodIdle : WavePeriod;

        _waveT = Mathf.PosMod(_waveT + (float)delta / period, 1f);
        var scale = Mathf.Lerp(1f, WaveScaleMax, _waveT);
        _layer4.Scale = new Vector2(scale, scale);
        _layer4.Modulate = new Color(1f, 1f, 1f, 1f - _waveT);
    }
}
