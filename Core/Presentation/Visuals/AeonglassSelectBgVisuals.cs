using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Visuals;

/// <summary>
/// 选人场景（<c>STS2_Aeonglass/scenes/screens/char_select/char_select_bg_aeonglass.tscn</c>）的根节点脚本。
/// </summary>
/// <remarks>
/// <para>
/// 存在的理由：<b>把人物骨架播起来</b>。场景里的 <c>SpineSprite</c> 只写了
/// <c>preview_animation = "idle_loop"</c>，而那是<b>编辑器预览</b>属性 ——
/// 运行时实测（headless 跑 96 帧）<c>get_animation_state().get_current(0)</c> 始终为 <c>null</c>、
/// <c>get_bounds()</c> 一直停在 setup pose，也就是<b>不挂脚本就是一张静止的 setup pose</b>。
/// </para>
/// <para>
/// 另外：本体选人场景那条线的驱动脚本是 <c>res://src/Core/Nodes/Animation/NSpineAutoPlayer.cs</c>，
/// 那是<b>游戏内的路径</b>，mod 的 <c>.tscn</c> 引用不到（编辑器会报缺脚本）—— 所以自带一个。
/// </para>
/// <para>
/// 播两条轨道，与战斗里 <see cref="AeonglassCreatureVisuals" /> 的做法一致：
/// 轨道 0 = 身体 <c>idle_loop</c>，轨道 1 = <c>_track1/rings_normal</c>（外环持续旋转）。
/// 骨架里的外环旋转是**独立叠加动画**，只播轨道 0 的话环是死的。
/// </para>
/// <para>
/// <b>播哪条动画由本节点上的三个可导出属性决定</b>（<see cref="BodyAnimation" /> /
/// <see cref="RingAnimation" /> / <see cref="Loop" />）—— 在 Godot 检查器或 <c>.tscn</c> 里
/// 直接改就行，不用动代码、也不用换节点。名字填错会打一条警告并回落到 <c>idle_loop</c>。
/// </para>
/// <para>
/// ⚠️ <b>Godot 编辑器里看到的环一定是「散开」的，这是预览的局限，不是 bug</b>：
/// <c>SpineSprite.preview_animation</c> 只作用在<b>轨道 0</b>，而外环归位靠的是<b>轨道 1</b>的
/// <c>_track1/rings_normal</c>。实测包围盒：只播 <c>idle_loop</c> 时外环甩在右边（x 一直到 3372），
/// 加上 <c>rings_normal</c> 才收成一个环绕本体的大环（x -1098..1086）。
/// 编辑器里想核对构图，把 <c>preview_animation</c> 临时改成 <c>_track1/rings_normal</c> 即可
/// （只影响编辑器显示，运行时由上面三个属性决定）。
/// </para>
/// <para>
/// ⚠️ <b>别用 <c>wither</c></b>（<see cref="BodyAnimation" /> 或 <c>preview_animation</c> 都不要）：
/// 骨架的 <c>wither</c> 姿态里有附件引用了 atlas 中<b>不存在</b>的 region，spine 会回退画成
/// 占位贴图 —— 也就是 <c>images/character/spine/aeonglass_2.png</c> 那张粉白方格
/// （那张 PNG 是 atlas 的第 2 页，整页就一个 <c>attack_tex/blast_placeholder</c>）。
/// 离线渲染实测（1920×1080，逐像素找粉白）：<c>setup</c> / <c>idle_loop</c> /
/// <c>_track1/rings_normal</c> 三种姿态都是 0 个占位像素，<b>只有 <c>wither</c> 会在角色顶部露出方格</b>。
/// </para>
/// </remarks>
public partial class AeonglassSelectBgVisuals : Control
{
    /// <summary>轨道 0（身体）播的动画名。骨架里必须存在，否则警告并回落到 <c>idle_loop</c>。</summary>
    [Export]
    public string BodyAnimation { get; set; } = AeonglassAnimNames.Idle;

    /// <summary>轨道 1（外环叠加）播的动画名。<b>留空 = 这条轨道不播</b>。</summary>
    [Export]
    public string RingAnimation { get; set; } = AeonglassAnimNames.RingsNormal;

    /// <summary>是否循环播放（两条轨道共用）。想看一次性动作（如 <c>attack_double</c>）就关掉。</summary>
    [Export]
    public bool Loop { get; set; } = true;

    public override void _Ready()
    {
        var found = 0;

        foreach (var child in GetChildren())
        {
            // 骨架节点是 GDExtension 的原生类型，C# 侧没有对应类，只能按原生类名认。
            if (child is not Node2D node || node.GetClass() != "SpineSprite")
            {
                continue;
            }

            found++;
            var sprite = new MegaSprite(node);

            this.RunWhenSpineReady(sprite, state =>
            {
                Play(sprite, state, BodyAnimation, trackId: 0, fallback: AeonglassAnimNames.Idle);
                Play(sprite, state, RingAnimation, trackId: 1, fallback: null);

                // 自证日志：编辑器预览看不到轨道 1，实机里看这一行就能确认两条轨道真的落地了。
                Const.Logger.Info(
                    $"[{Const.ModId}] 选人人物动画已下发：轨道0='{state.GetCurrentAnimationName(0) ?? "(无)"}'，"
                    + $"轨道1='{state.GetCurrentAnimationName(1) ?? "(无)"}'");
            });
        }

        if (found == 0)
        {
            Const.Logger.Warn($"[{Const.ModId}] 选人场景里没有 SpineSprite，人物不会动。");
        }
    }

    /// <summary>在指定轨道上播一条动画；留空跳过，名字不存在则警告并（可选）回落到 <paramref name="fallback" />。</summary>
    private void Play(MegaSprite sprite, MegaAnimationState state, string? animation, int trackId, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(animation))
        {
            return;
        }

        if (sprite.HasAnimation(animation))
        {
            state.SetAnimation(animation, loop: Loop, trackId: trackId);
            return;
        }

        Const.Logger.Warn($"[{Const.ModId}] 骨架里没有动画 '{animation}'（轨道 {trackId}）。");

        if (fallback is not null && sprite.HasAnimation(fallback))
        {
            state.SetAnimation(fallback, loop: Loop, trackId: trackId);
        }
    }
}
