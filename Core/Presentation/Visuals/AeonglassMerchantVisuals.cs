using Godot;

using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

using STS2_Aeonglass.Core.Presentation.Animations;

namespace STS2_Aeonglass.Core.Presentation.Visuals;

/// <summary>永世沙漏在<b>商店</b>里的形象（挂在 <c>scenes/shop/aeonglass_merchant.tscn</c> 上）。</summary>
/// <remarks>
/// <para>
/// 本体 <see cref="NMerchantCharacter" /> 的 <c>_Ready</c> 做了两件事：① 把<b>第 0 个子节点</b>当 SpineSprite；
/// ② 播它写死的 <c>relaxed_loop</c>（循环）。我们的骨架里**没有** <c>relaxed_loop</c> ——
/// 按用户要求，商店占位**一律用 <c>idle_loop</c>**。
/// </para>
/// <para>
/// 所以这里<b>故意不调 <c>base._Ready()</c></b>，而是自己复刻那两行、把动画名换成
/// <see cref="AeonglassAnimNames.Idle" />：基类的播放逻辑内联在 <c>_Ready</c> 里、
/// <c>PlayAnimation</c> 又是 public 而非 virtual，先调 base 再覆盖会变成"靠回调顺序取胜"，
/// 不如直接不注册基类那一次。
/// </para>
/// <para>
/// <b>代价</b>：将来本体若往 <c>NMerchantCharacter._Ready</c> 里加东西，我们不会跟着走
/// （风险低 —— 这个方法从 0.111.0 起就只有这两件事）。
/// </para>
/// </remarks>
public partial class AeonglassMerchantVisuals : NMerchantCharacter
{
    public override void _Ready()
    {
        if (GetChildCount() == 0)
        {
            Const.Logger.Warn($"[{Const.ModId}] 商店场景没有子节点，无法播动画。");
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
