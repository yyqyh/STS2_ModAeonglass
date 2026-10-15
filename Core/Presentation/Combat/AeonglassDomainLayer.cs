using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace STS2_Aeonglass.Core.Presentation.Combat;
/// <summary>「领域」战斗特效层（挂在 <c>scenes/combat/aeonglass_domain.tscn</c> 上）。</summary>
/// <remarks>
/// ① 职责：背景与人之间的整层画面 —— 透明空槽贴图 + <b>圆扫描揭示</b>。
/// ② 调用时机：由 <see cref="AeonglassDomainLayerHost" /> 插进战斗房间；目前<b>常启</b>。
/// ③ 依赖：圆蒙版参数（圆心起点 / 半径 / 羽化）+ 与第三章背景同画布的空槽图。
/// ④ ⚠️ 极性是"圆内透明、圆外可见"，写反了整层看不见；要按机制开关就把 <c>Visible</c> 交给机制层。
/// </remarks>

[Tool]
public partial class AeonglassDomainLayer : Control
{
    /// <summary>节点名（宿主用它做幂等判断）。</summary>
    public const string NodeName = "AeonglassDomainLayer";

    /// <summary>蒙版进度 uniform 名。</summary>
    private const string ProgressParam = "progress";

    /// <summary>场景里那条擦除动画的名字。</summary>
    private const string WipeAnimation = "wipe";

    /// <summary>接在擦除后面、"在上下两个中点之间来回"的循环动画名。</summary>
    private const string BreatheAnimation = "breathe";

    private ShaderMaterial? _material;
    private AnimationPlayer? _anim;
    private float _progress;

    /// <summary>
    /// 蒙版进度 —— <b>给 AnimationPlayer 打关键帧用的口子</b>（普通属性轨，编辑器里能直接认出来）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>0</c> = 圆心在背景右上角（圆盖满整幅）；<c>1</c> = 圆心右移一个画布宽。
    /// 当前极性是"圆内透明、圆外可见"，所以 <c>0</c> 时整层不可见、数值变大 = 领域从左侧被揭出来。
    /// </para>
    /// <para>
    /// 为什么不用"打 <c>material:shader_parameter/progress</c> 资源子路径"那种写法：
    /// 那种轨在编辑器里不好认、也不好手加。给脚本属性打帧最稳，setter 里再写进 shader。
    /// </para>
    /// </remarks>
    [Export(PropertyHint.Range, "0,1,0.001")]
    public float MaskProgress
    {
        get => _progress;
        set
        {
            _progress = Mathf.Clamp(value, 0f, 1f);
            ApplyProgress();
        }
    }

    public override void _Ready()
    {
        _material = GetNodeOrNull<TextureRect>("Layer")?.Material as ShaderMaterial;
        _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        if (_material is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [领域] 找不到 Layer 上的蒙版材质，擦除动画会没有效果。");
        }

        // 起始 = 圆心在背景右上角（当前极性下 = 整层不可见）。
        // 先补写一次：万一 MaskProgress 在 _Ready 之前就被 set 过（那时 _material 还是 null），这里能把值落到材质上。
        ApplyProgress();

        // 游戏里永远是"从头开始"（起始 = progress 0 = 整层不可见，等机制触发 wipe）。
        // 编辑器里**不动**：这样拖动画播放头看到的状态不会被下一帧覆盖回去（[Tool] 下 setter 才生效）。
        if (!Engine.IsEditorHint())
        {
            MaskProgress = 0f;
        }
    }

    /// <summary>
    /// 直接设蒙版进度：<c>0</c> = 圆心在背景右上角（整幅可见）；
    /// <c>1</c> = 圆心右移一个画布宽、圆边掠过右下角（领域被推走）。
    /// </summary>
    /// <param name="progress">目标进度。</param>
    public void SetProgress(float progress)
    {
        _anim?.Stop();
        MaskProgress = progress;
    }

    /// <summary>把当前进度写进材质（动画打帧时走这里）。</summary>
    private void ApplyProgress() => _material?.SetShaderParameter(ProgressParam, _progress);

    /// <summary>
    /// 播"圆心向右移"的擦除：<c>wipe</c> 走完之后<b>自动接 <c>breathe</c></b>
    /// （在"圆边过上边中点 ↔ 过下边中点"之间正逆循环，不再停）。
    /// </summary>
    /// <param name="reverse">倒放：把领域收回去（只播 <c>wipe</c> 的反向，不接循环）。</param>
    public void PlayWipe(bool reverse = false)
    {
        if (_anim is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [领域] 场景里没有 AnimationPlayer，擦除动画播不了。");
            return;
        }

        _anim.Stop();

        if (reverse)
        {
            _anim.PlayBackwards(WipeAnimation);
            return;
        }
        else
        {
            _anim.Play(WipeAnimation);
        }

        // 擦完直接进"正逆循环"（用户口径：从完成态接着走到过上边中点，然后来回）。
        _anim.Queue(BreatheAnimation);
    }

    /// <summary>直接进"正逆循环"（跳过前面的擦除）。</summary>
    public void PlayBreathe()
    {
        if (_anim is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [领域] 场景里没有 AnimationPlayer。");
            return;
        }

        _anim.Play(BreatheAnimation);
    }

    // ================== 静态口子（给机制层用） ==================

    /// <summary>开关这一层（默认常启；给将来的"领域"机制留的口子）。</summary>
    public static void SetEnabled(bool enabled)
    {
        if (TryGet(out var layer))
        {
            layer!.Visible = enabled;
        }
    }

    /// <summary>播一次擦除动画（<paramref name="reverse" /> = 倒放）。</summary>
    public static void PlayWipeOnCurrent(bool reverse = false)
    {
        if (TryGet(out var layer))
        {
            layer!.PlayWipe(reverse);
        }
    }

    /// <summary>直接进"正逆循环"。</summary>
    public static void PlayBreatheOnCurrent()
    {
        if (TryGet(out var layer))
        {
            layer!.PlayBreathe();
        }
    }

    /// <summary>找当前战斗里活着的那一层。</summary>
    private static bool TryGet(out AeonglassDomainLayer? layer)
    {
        layer = null;

        if (NCombatRoom.Instance is { } room
            && room.GetNodeOrNull<AeonglassDomainLayer>($"CombatSceneContainer/{NodeName}") is { } found)
        {
            layer = found;
            return true;
        }

        return false;
    }
}
