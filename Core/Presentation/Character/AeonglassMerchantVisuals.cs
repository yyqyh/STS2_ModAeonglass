using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Character;
/// <summary>商店里的角色外观（<c>scenes/shop/aeonglass_merchant.tscn</c> 根脚本）。</summary>
/// <remarks>
/// ① 职责：商店场景中的立绘/骨架展示。
/// ② 调用时机：本体商店实例化角色时。
/// ③ 依赖：Spine 骨架 + <c>Const.Paths</c> 里的空槽贴图。
/// ④ ⚠️ 图是透明空槽，出图后同名覆盖即可；不要改场景根节点名。
/// </remarks>

public partial class AeonglassMerchantVisuals : NMerchantCharacter
{
    public override void _Ready()
    {
        if (GetChildCount() == 0)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Host} 商店场景没有子节点，无法播动画。");
            return;
        }

        // 第 0 个子节点必须是 SpineSprite（本体按索引取，不是按名字）。
        var spine = new MegaSprite(GetChild(0));

        this.RunWhenSpineReady(spine, state =>
        {
            PlayAnimation(AeonglassAnimNames.Idle, loop: true);

            // 轨道 1：外环归位并旋转。不播的话外环会停在被甩出去的 setup 位置（跟战斗 / 选人一致）。
            if (spine.HasAnimation(AeonglassAnimNames.RingsNormal))
            {
                state.SetAnimation(AeonglassAnimNames.RingsNormal, loop: true, trackId: 1);
            }
        });
    }
}
