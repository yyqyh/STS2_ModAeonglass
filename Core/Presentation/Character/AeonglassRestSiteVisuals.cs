using System.Linq;

using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.RestSite;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Character;
/// <summary>火堆（休息处）里的角色外观（<c>scenes/rest_site/aeonglass_rest_site.tscn</c> 根脚本）。</summary>
/// <remarks>
/// ① 职责：火堆场景中的角色展示（本体 <c>NRestSiteCharacter</c>）。
/// ② 调用时机：本体进火堆房间时实例化。
/// ③ 依赖：Spine 骨架 + 空槽贴图。
/// ④ ⚠️ 出图后同名覆盖；节点名与场景引用保持一致。
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
