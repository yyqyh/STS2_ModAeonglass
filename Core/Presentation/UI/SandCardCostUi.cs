using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// 卡面左上角的「消耗沙砾」费用显示（挂在 <c>scenes/ui/secondary_resource/aeonglass_sand_card_cost.tscn</c> 上）。
/// </summary>
/// <remarks>
/// <para>
/// 根节点就是 RitsuLib 的 <see cref="NSecondaryResourceCardCostUi" />：它自己按<b>这张卡的次级资源费用</b>
/// 决定显不显示、显示几、可负担还是不可负担（颜色由样式给）。
/// 我们只在这个场景里定<b>位置与槽位尺寸</b>，并在 <c>_Ready</c> 里绑定"沙砾"这个资源 + 套样式。
/// </para>
/// <para>
/// 图标就是注册资源时给的 <c>smallIconPath</c>（现在是空槽图）。
/// 卡牌侧声明费用用 <c>this.SecondaryCosts().Set(AeonglassSecondaryResources.SandId, n)</c>。
/// </para>
/// </remarks>
public partial class SandCardCostUi : NSecondaryResourceCardCostUi
{
    public override void _Ready()
    {
        base._Ready();

        Configure(AeonglassSecondaryResources.SandCardCostStyle);

        if (AeonglassSecondaryResources.Sand is { } sand)
        {
            Bind(sand.Id);
        }
    }
}
