using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Presentation.SecondaryResource;
/// <summary>卡面左上角的「消耗沙砾」费用显示（脚本挂在 <c>aeonglass_sand_card_cost.tscn</c> 上）。</summary>
/// <remarks>
/// ① 职责：把"这张卡要花几点沙砾"画到卡面。
/// ② 调用时机：RitsuLib 次级资源费用 UI 构建时。
/// ③ 依赖：RitsuLib 的次级资源卡费基类 + 样式 <c>SandResourceRegistry.SandCardCostStyle</c>。
/// ④ ⚠️ 尺寸 / 配色改<b>系统层样式</b>，不在本文件里写死。
/// </remarks>

public partial class AeonglassSandCardCostUi : NSecondaryResourceCardCostUi
{
    public override void _Ready()
    {
        base._Ready();

        Configure(SandResourceRegistry.SandCardCostStyle);

        if (SandResourceRegistry.Sand is { } sand)
        {
            Bind(sand.Id);
        }
    }
}
