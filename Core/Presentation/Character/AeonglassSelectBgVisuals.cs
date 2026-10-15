using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Character;
/// <summary>选人界面背景（<c>scenes/screens/char_select/char_select_bg_aeonglass.tscn</c> 根脚本）。</summary>
/// <remarks>
/// ① 职责：选人场景的背景视觉/入场。
/// ② 调用时机：选人界面加载本角色背景时。
/// ③ 依赖：<c>Const.Paths</c> 把该场景交给角色（本体按角色取）。
/// ④ ⚠️ 出图后同名覆盖；根节点名别改（本体按名字取节点）。
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
                    $"[{Const.ModId}] {Const.Prefix.Host} 选人人物动画已下发：轨道0='{state.GetCurrentAnimationName(0) ?? "(无)"}'，"
                    + $"轨道1='{state.GetCurrentAnimationName(1) ?? "(无)"}'");
            });
        }

        if (found == 0)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Host} 选人场景里没有 SpineSprite，人物不会动。");
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

        Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Host} 骨架里没有动画 '{animation}'（轨道 {trackId}）。");

        if (fallback is not null && sprite.HasAnimation(fallback))
        {
            state.SetAnimation(fallback, loop: Loop, trackId: trackId);
        }
    }
}
