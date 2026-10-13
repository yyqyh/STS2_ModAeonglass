using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Visuals;

/// <summary>
/// 永世沙漏的**战斗外观根节点**（挂在 <c>STS2_Aeonglass/scenes/combat/aeonglass_combat.tscn</c> 上）。
/// </summary>
/// <remarks>
/// <para>
/// 存在的第一个理由：让 <c>.tscn</c> 挂<b>我们自己的脚本</b>，而不是引用游戏内的
/// <c>res://src/Core/Nodes/Combat/NCreatureVisuals.cs</c> —— 后者在 mod 的 Godot 工程里加载不出来，
/// 编辑器会报缺脚本。
/// </para>
/// <para>
/// 第二个理由：把<b>轨道 1</b>的 <c>_track1/rings_normal</c> 播起来 —— 沙漏的外环在转全靠它。
/// 这是纯 Spine 动画（无 shader / 无粒子），是 ① 里唯一接的表现层东西。
/// </para>
/// <para>
/// 本体 <c>NCreatureVisuals</c> 在 <c>_Ready</c> 里用 <c>GetNode&lt;Node2D&gt;("%Visuals")</c> 取身体节点，
/// 所以场景里那个节点必须叫 <c>Visuals</c> 且勾了 <c>unique_name_in_owner</c>。
/// Godot 的 <c>_Ready</c> 是自下而上调用 —— 我们的 <c>_Ready</c> 跑的时候子节点 <c>Visuals</c> 已经就绪，
/// 而 <c>base._Ready()</c> 也已经把 <see cref="NCreatureVisuals.SpineBody" /> 建好了。
/// </para>
/// </remarks>
public partial class AeonglassCreatureVisuals : NCreatureVisuals
{
    /// <summary>取某个生物的战斗外观；不是永世沙漏（或不在战斗里）时返回 null。</summary>
    public static AeonglassCreatureVisuals? Of(Creature? creature)
        => NCombatRoom.Instance?.GetCreatureNode(creature)?.Visuals as AeonglassCreatureVisuals;

    public override void _Ready()
    {
        base._Ready();

        try
        {
            PlayTrack1Rings();
        }
        catch (Exception ex)
        {
            // 播不出来只打一条警告：绝不能因为表现层问题把战斗搞崩。
            Const.Logger.Warn($"[{Const.ModId}] 轨道 1 环旋转启动失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 在<b>轨道 1</b> 上播外环旋转。
    /// </summary>
    /// <remarks>
    /// 与角色状态机在<b>轨道 0</b> 上播的身体动画互不干扰（原版 boss 就是两条轨道并行：
    /// <c>NAeonGlassVfx</c> 在轨道 1 播 <c>_track1/rings_*</c>，<c>CreatureAnimator</c> 在轨道 0 播身体）。
    /// </remarks>
    public void PlayTrack1Rings()
    {
        if (SpineBody is not { } spine)
        {
            return;
        }

        if (!spine.HasAnimation(AeonglassAnimNames.RingsNormal))
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] 骨架里找不到 '{AeonglassAnimNames.RingsNormal}' —— 外环不会转。");
            return;
        }

        if (spine.TryGetAnimationState() is not { } state)
        {
            return;
        }

        state.SetAnimation(AeonglassAnimNames.RingsNormal, loop: true, trackId: 1);
    }

    /// <summary>调试面板用：轨道 1 当前动画名。</summary>
    public string? CurrentTrack1Animation => SpineAnimation.GetCurrentAnimationName(1);

    /// <summary>调试面板用：轨道 0 当前动画名。</summary>
    public string? CurrentTrack0Animation => SpineAnimation.GetCurrentAnimationName(0);
}
