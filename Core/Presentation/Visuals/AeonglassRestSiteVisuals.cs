using System.Linq;

using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.RestSite;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Visuals;

/// <summary>永世沙漏在<b>休息处（火堆）</b>里的形象（挂在 <c>scenes/rest_site/aeonglass_rest_site.tscn</c> 上）。</summary>
/// <remarks>
/// <para>
/// 本体 <see cref="NRestSiteCharacter" /> 的 <c>_Ready</c> 有硬要求：<c>ControlRoot</c>(Control)、
/// <c>%Hitbox</c>(Control)、<c>%SelectionReticle</c>(NSelectionReticle)、
/// <c>%ThoughtBubbleLeft</c> / <c>%ThoughtBubbleRight</c>(Control) ——
/// 后三个由 RitsuLib 的 <c>RitsuNRestSiteCharacterNodeFactory</c> 在运行时补，
/// 所以场景里只放 <c>ControlRoot</c> + <c>%Hitbox</c>（照小丑的做法；
/// **不要在 tscn 里 instance <c>res://scenes/ui/selection_reticle.tscn</c>**：跨 pck 烘焙会失败）。
/// </para>
/// <para>
/// 它还会给根下**所有直接 SpineSprite 子节点**按章节播 <c>overgrowth_loop</c> / <c>hive_loop</c> /
/// <c>glory_loop</c> —— 我们的骨架里一个都没有。按用户要求，休息处占位**一律用 <c>idle_loop</c>**。
/// </para>
/// <para>
/// <b>这里选择「先调 base、再覆盖」而不是重写 <c>_Ready</c></b>：基类那次 <c>_Ready</c> 还负责
/// 绑定 Hitbox 的焦点/鼠标事件、记住 thought bubble 锚点，重写等于把这些都抄一遍（脆）。
/// 覆盖是安全的，理由：<c>RunWhenSpineReady</c> 是「每帧轮询 + 就绪后回调」，
/// 基类那次先注册、我们这次后注册，两者等待的帧数完全相同，
/// 因此**回调严格按注册顺序执行 → 我们的 <c>idle_loop</c> 一定最后落地**。
/// </para>
/// </remarks>
public partial class AeonglassRestSiteVisuals : NRestSiteCharacter
{
    public override void _Ready()
    {
        base._Ready();

        foreach (var spine in GetChildren().OfType<Node2D>())
        {
            if (spine.GetClass() != "SpineSprite")
            {
                continue;
            }

            var sprite = new MegaSprite(spine);

            this.RunWhenSpineReady(sprite, state =>
            {
                // 覆盖基类按章节选的 overgrowth_loop / hive_loop / glory_loop（我们没有这三个动作）。
                state.SetAnimation(AeonglassAnimNames.Idle, loop: true);

                // 轨道 1：外环归位并旋转（跟战斗 / 选人 / 商店一致）。
                if (sprite.HasAnimation(AeonglassAnimNames.RingsNormal))
                {
                    state.SetAnimation(AeonglassAnimNames.RingsNormal, loop: true, trackId: 1);
                }
            });
        }
    }
}
