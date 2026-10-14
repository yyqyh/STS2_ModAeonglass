using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// 「领域」战斗特效层（挂在 <c>scenes/combat/aeonglass_domain.tscn</c> 上）。
/// </summary>
/// <remarks>
/// <para>
/// <b>落点</b>：战斗房间的结构是
/// <c>CombatRoom/CombatSceneContainer/{BgContainer, BackCombatVfxContainer, AllyContainer, EnemyContainer}</c>，
/// 绘制序 = 树序。本层被插到 <see cref="CombatSceneContainer" /> 的 <b>index 1</b>
/// ⇒ 正好在 <c>BgContainer</c>（背景）之后、<c>BackCombatVfxContainer</c> / 人物之前
/// （用户 2026-10-14 口径："在 bgcontainer 和 backcombatvfxcontainer 之间加一个领域层"）。
/// </para>
/// <para>
/// <b>目前常启</b>（用户口径）：不做开关、不判断任何状态，只要打的是本角色就挂上。
/// 贴图是<b>透明空槽</b>（<c>2048×960</c>，与本体第三章 glory 背景同画布），出图后同名覆盖即可。
/// </para>
/// <para>
/// 将来要按机制开关，建议把 <c>Visible</c> 交给机制层（本类只提供 <see cref="SetEnabled" /> 这个口子，
/// 不自己判断）。
/// </para>
/// <para>
/// <b>圆形蒙版</b>（用户 2026-10-14 口径）：圆心起点在**背景图的右上角** <c>(W, 0)</c>、
/// 半径取到**左下角**的对角线 <c>√(W²+H²)</c>（整段动画不变）；圆心**水平向右**移，
/// 圆边就从左上角往右下扫过去。边缘按 <c>feather_px</c> 羽化。
/// </para>
/// <para>
/// <b>极性已反</b>：圆**内**透明、圆**外**可见 ⇒ <c>progress = 0</c>（圆盖满整幅）时整层不可见，
/// 圆心右移 = "圆边扫过、领域从左侧被揭出来"。
/// </para>
/// <para>
/// <c>progress</c> 的换算（想"圆边正好掠过某点 T"）：
/// <c>c.x = T.x + √(r² − T.y²)</c>、<c>progress = (c.x − W) / W</c>。
/// 本场景两条动画用的档（W=2048, H=960, r≈2261.83）：
/// <list type="bullet">
///   <item><description><c>wipe</c>：0 → <b>0.5</b>（圆边掠过**下边中点**），0.8s，单次；</description></item>
///   <item><description><c>breathe</c>：<b>0.5 → 0.6044</b>（接着走到圆边掠过**上边中点**），0.9s，<b>正逆循环</b>（Ping-Pong）。</description></item>
/// </list>
/// 两条都是打 <c>Layer:material:shader_parameter/progress</c> 的值轨，
/// <b>在 Godot 编辑器里可以直接按播放预览</b>。
/// </para>
/// </remarks>
[Tool]
public partial class DomainLayer : Control
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
    private static bool TryGet(out DomainLayer? layer)
    {
        layer = null;

        if (NCombatRoom.Instance is { } room
            && room.GetNodeOrNull<DomainLayer>($"CombatSceneContainer/{NodeName}") is { } found)
        {
            layer = found;
            return true;
        }

        return false;
    }
}

/// <summary>把「领域」层挂进战斗房间（照其它 Host 的模式；随房间销毁）。</summary>
internal static class DomainLayerHost
{
    /// <summary>插入位置：<c>CombatSceneContainer</c> 的第 1 位 = BgContainer 之后、其余之前。</summary>
    private const int InsertIndex = 1;

    internal static void Attach(Node room)
    {
        if (room.GetNodeOrNull<Control>("CombatSceneContainer") is not { } sceneContainer)
        {
            Const.Logger.Warn($"[{Const.ModId}] 找不到 CombatSceneContainer ⇒「领域」层没挂上。");
            return;
        }

        if (sceneContainer.GetNodeOrNull<DomainLayer>(DomainLayer.NodeName) is not null)
        {
            return;
        }

        var layer = PreloadManager.Cache
            .GetScene(Const.Paths.Ui.DomainScene)
            .Instantiate<DomainLayer>(PackedScene.GenEditState.Disabled);

        layer.Name = DomainLayer.NodeName;
        sceneContainer.AddChild(layer);
        sceneContainer.MoveChild(layer, InsertIndex);

        // 用户口径 2026-10-14：**进战斗就自动播** —— wipe 把领域擦出来（progress 0 → 0.5），
        // 走完自动 Queue 到 breathe，在"过下边中点 ↔ 过上边中点"之间正逆循环。
        layer.PlayWipe();

        Const.Logger.Info(
            $"[{Const.ModId}] [领域] 特效层已挂入 CombatSceneContainer 第 {InsertIndex} 位"
            + "（BgContainer 之后、BackCombatVfxContainer / 人物之前）；已自动播 wipe → breathe 循环");
    }
}
