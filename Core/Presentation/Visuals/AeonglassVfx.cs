using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Visuals;

/// <summary>
/// 永世沙漏的战斗特效驱动（挂在战斗外观场景的 <c>Visuals/AeonglassVfx</c> 上）。
/// </summary>
/// <remarks>
/// <para>
/// <b>它是原版 boss 的 <c>MegaCrit.Sts2.Core.Nodes.Vfx.NAeonGlassVfx</c> 的玩家侧移植版</b>：
/// 行为照抄（连触发时机都一样），只改了三点 ——
/// </para>
/// <list type="number">
/// <item><description>
/// <b>节点查询改成容错</b>：原版用 <c>GetNode</c>，缺一个节点就在战斗里抛异常；
/// 这里用 <c>GetNodeOrNull</c> 并把缺失项收集起来（<see cref="MissingNodes" />），
/// 只打一条警告、其余特效照常跑。补美术 / 改结构时不会把战斗搞崩。
/// </description></item>
/// <item><description>
/// <b>轨道 1 的环动画名集中到 <see cref="AeonglassAnimNames" /></b>，不写裸字符串。
/// </description></item>
/// <item><description>
/// 多一个 <see cref="StatusLine" /> 给调试面板读（液体滚动速度 / 缺失节点）。
/// </description></item>
/// </list>
/// <para>
/// <b>它驱动的东西</b>（都在同一个 <c>Visuals</c> 下，坐标是骨架局部坐标，随 <c>Visuals.scale</c> 等比缩放）：
/// </para>
/// <list type="bullet">
/// <item><description><c>LiquidSlot</c>：<c>scrolling_smoke</c> shader 让沙漏里的液体滚动（<c>ScrollSpeed</c> 参数）；</description></item>
/// <item><description><c>RaySlot</c>：<c>aeonglass_ray_shader</c>（眼棱射线，参数由动画事件 <c>fire_ray</c> 之外的外部逻辑驱动，本移植版保持原样）；</description></item>
/// <item><description><c>GlassCenterSlot</c>（倾倒 / 碎裂）、<c>WitherSlot</c>（凋萎红点）、<c>TopSparksSlot</c> / <c>BottomSparksSlot</c>（火花）、<c>GroundPlowSlot</c>（刮地扬尘）。</description></item>
/// </list>
/// <para>
/// <b>动画事件</b>（骨架自带，玩家侧同样会触发）：
/// <c>start_wither</c> / <c>end_wither</c> / <c>start_die</c> / <c>end_die</c> /
/// <c>start_sparks</c> / <c>start_ground_scrape</c> / <c>end_ground_scrape</c>。
/// 玩家侧永远不播"刮地"那套动作，所以 <c>GroundPlow</c> 的两组粒子实际上不会开火 —— 保留是为了"一整套"完整。
/// </para>
/// </remarks>
public partial class AeonglassVfx : Node
{
    private static readonly StringName ScrollSpeedParam = new("ScrollSpeed");

    private Node2D _parent = null!;
    private MegaSprite _animController = null!;
    private ShaderMaterial? _liquidShaderMat;
    private float _baseScrollSpeed;

    private bool _ringsSpinningNormal;
    private string? _curAnimName;

    private GpuParticles2D? _witherParticles;
    private GpuParticles2D? _leakParticles;
    private GpuParticles2D? _shardParticles;
    private GpuParticles2D? _dumpParticles;
    private GpuParticles2D? _topSparkParticles;
    private GpuParticles2D? _bottomSparkParticles;
    private GpuParticles2D? _groundDustParticles;
    private GpuParticles2D? _groundChunkParticles;

    /// <summary>场景里没找到的节点路径（调试面板会显示；空 = 全部就位）。</summary>
    public IReadOnlyList<string> MissingNodes { get; private set; } = [];

    /// <summary>一行状态（给调试面板）。</summary>
    public string StatusLine =>
        $"{(_liquidShaderMat is null ? "液体shader=缺失" : $"ScrollSpeed={GetScrollSpeed():0.##}")}"
        + $" ｜ 粒子={(MissingNodes.Count == 0 ? "齐全" : $"缺 {MissingNodes.Count}")}";

    public override void _Ready()
    {
        _parent = GetParent<Node2D>();
        _animController = new MegaSprite(_parent);

        var missing = new List<string>();

        var liquidSlot = FindNode<Node>("LiquidSlot", missing);
        if (liquidSlot is not null)
        {
            _liquidShaderMat = new MegaSlotNode(liquidSlot).GetNormalMaterial() as ShaderMaterial;
        }

        if (_liquidShaderMat is null)
        {
            missing.Add("LiquidSlot(ShaderMaterial)");
        }
        else
        {
            _baseScrollSpeed = GetScrollSpeed();
        }

        _witherParticles = FindNode<GpuParticles2D>("WitherSlot/WitherParticles", missing);
        _leakParticles = FindNode<GpuParticles2D>("LiquidSlot/LeakParticles", missing);
        _shardParticles = FindNode<GpuParticles2D>("GlassCenterSlot/ShardParticles", missing);
        _dumpParticles = FindNode<GpuParticles2D>("GlassCenterSlot/DumpParticles", missing);
        _topSparkParticles = FindNode<GpuParticles2D>("TopSparksSlot/TopSparkParticles", missing);
        _bottomSparkParticles = FindNode<GpuParticles2D>("BottomSparksSlot/BottomSparkParticles", missing);
        _groundDustParticles = FindNode<GpuParticles2D>("GroundPlowSlot/DustParticles", missing);
        _groundChunkParticles = FindNode<GpuParticles2D>("GroundPlowSlot/ChunkParticles", missing);

        foreach (var oneShot in new[]
                 {
                     _bottomSparkParticles, _topSparkParticles, _witherParticles,
                     _shardParticles, _dumpParticles,
                 })
        {
            if (oneShot is not null)
            {
                oneShot.OneShot = true;
            }
        }

        MissingNodes = missing;
        if (missing.Count > 0)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] 特效节点缺失（其余特效照常）：{string.Join(" / ", missing)}");
        }

        ResetVfx();

        // 骨架还没就绪时不能碰动画状态（Godot 的 _Ready 是自下而上的，我们的 _Ready 早于父 SpineSprite）。
        this.RunWhenSpineReady(_animController, state =>
            state.SetAnimation(AeonglassAnimNames.RingsNormal, loop: true, trackId: 1));

        _animController.ConnectAnimationEvent(
            Callable.From<GodotObject, GodotObject, GodotObject, GodotObject>(OnAnimationEvent));
        _animController.ConnectAnimationStarted(
            Callable.From<GodotObject, GodotObject, GodotObject>(OnAnimationStart));
    }

    // ============================ 动画事件（骨架自带） ============================

    private void OnAnimationEvent(GodotObject _, GodotObject __, GodotObject ___, GodotObject spineEvent)
    {
        var eventName = new MegaEvent(spineEvent).GetData().GetEventName();
        switch (eventName)
        {
            case "start_wither":
                // 施法：液体加速滚动 + 凋萎红点开火。
                _witherParticles?.Restart();
                _liquidShaderMat?.SetShaderParameter(ScrollSpeedParam, -2f);
                break;
            case "end_wither":
                ResetVfx();
                break;
            case "start_die":
                // 死亡：玻璃碎裂 + 液体泄漏。
                _dumpParticles?.Restart();
                _leakParticles?.Restart();
                _shardParticles?.Restart();
                break;
            case "end_die":
                if (_leakParticles is not null)
                {
                    _leakParticles.Emitting = false;
                }

                break;
            case "start_sparks":
                _topSparkParticles?.Restart();
                _bottomSparkParticles?.Restart();
                break;
            case "start_ground_scrape":
                _groundChunkParticles?.Restart();
                _groundDustParticles?.Restart();
                break;
            case "end_ground_scrape":
                if (_groundChunkParticles is not null)
                {
                    _groundChunkParticles.Emitting = false;
                }

                if (_groundDustParticles is not null)
                {
                    _groundDustParticles.Emitting = false;
                }

                break;
        }
    }

    // ============================ 轨道 1：外环 ============================

    private void OnAnimationStart(GodotObject spineSprite, GodotObject animationState, GodotObject trackEntry)
    {
        ResetVfx();

        var current = _animController.GetAnimationState().GetCurrentAnimationName();
        if (current == _curAnimName)
        {
            return;
        }

        _curAnimName = current;

        switch (current)
        {
            case AeonglassAnimNames.Idle:
            case AeonglassAnimNames.Hurt:
            case AeonglassAnimNames.Wither:
                if (!_ringsSpinningNormal)
                {
                    _animController.GetAnimationState()
                        .SetAnimation(AeonglassAnimNames.RingsNormal, loop: true, trackId: 1);
                }

                _ringsSpinningNormal = true;
                break;
            case AeonglassAnimNames.AttackHeavy:
                SwitchRings(AeonglassAnimNames.RingsAttackHeavy);
                break;
            case AeonglassAnimNames.AttackDouble:
                SwitchRings(AeonglassAnimNames.RingsAttackDouble);
                break;
            case AeonglassAnimNames.Die:
                _animController.GetAnimationState()
                    .SetAnimation(AeonglassAnimNames.RingsDie, loop: false, trackId: 1);
                _ringsSpinningNormal = false;
                break;
        }
    }

    /// <summary>攻击时先切到该攻击专属的环表现，播完接回常规旋转。</summary>
    private void SwitchRings(string attackRings)
    {
        var state = _animController.GetAnimationState();
        state.SetAnimation(attackRings, loop: false, trackId: 1);
        state.AddAnimation(AeonglassAnimNames.RingsNormal, 0f, loop: true, trackId: 1);
        _ringsSpinningNormal = false;
    }

    // ============================ 小工具 ============================

    private void ResetVfx()
    {
        _liquidShaderMat?.SetShaderParameter(ScrollSpeedParam, _baseScrollSpeed);

        if (_witherParticles is not null)
        {
            _witherParticles.Restart();
            _witherParticles.Emitting = false;
        }

        if (_leakParticles is not null)
        {
            _leakParticles.Restart();
            _leakParticles.Emitting = false;
        }

        if (_shardParticles is not null)
        {
            _shardParticles.Emitting = false;
        }

        if (_dumpParticles is not null)
        {
            _dumpParticles.Restart();
            _dumpParticles.Emitting = false;
        }

        if (_topSparkParticles is not null)
        {
            _topSparkParticles.Emitting = false;
        }

        if (_bottomSparkParticles is not null)
        {
            _bottomSparkParticles.Emitting = false;
        }

        if (_groundChunkParticles is not null)
        {
            _groundChunkParticles.Emitting = false;
        }

        if (_groundDustParticles is not null)
        {
            _groundDustParticles.Emitting = false;
        }
    }

    private float GetScrollSpeed()
        => _liquidShaderMat is null ? 0f : (float)_liquidShaderMat.GetShaderParameter(ScrollSpeedParam);

    private T? FindNode<T>(string path, List<string> missing) where T : class
    {
        var node = _parent.GetNodeOrNull<T>(path);
        if (node is null)
        {
            missing.Add(path);
        }

        return node;
    }
}
