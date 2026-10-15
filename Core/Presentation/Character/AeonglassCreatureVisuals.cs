using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Character;

/// <summary>永世沙漏的<b>战斗外观根节点</b>（挂在 <c>scenes/combat/aeonglass_combat.tscn</c> 上）。</summary>
/// <remarks>
/// ① 职责：承接本体 <c>NCreatureVisuals</c> 的四态动画与朝向。
/// ② 调用时机：本体创建角色视觉时实例化（战斗/多人与否由 <c>PlaceholderCharacterId</c> 补齐）。
/// ③ 依赖：场景里 <c>Visuals</c>(SpineSprite) / <c>Bounds</c> / <c>CenterPos</c> / <c>IntentPos</c> 四个 <c>unique_name_in_owner</c> 节点（<b>缺一个本体就抛异常</b>）；动画名见 <c>AeonglassAnimNames</c>。
/// ④ ⚠️ 特效驱动挂在子节点 <c>Visuals/AeonglassVfx</c>；改场景结构要同步本类与特效驱动。
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
