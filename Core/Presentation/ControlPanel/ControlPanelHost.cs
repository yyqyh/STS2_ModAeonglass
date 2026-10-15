using Godot;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Presentation.ControlPanel;
/// <summary>把控制面板挂进战斗房间（并接热键）。</summary>
/// <remarks>
/// ① 职责：只负责挂载与热键注册（幂等）。
/// ② 调用时机：<c>CombatUiMountPatch</c> 战斗房间 <c>_Ready</c>。
/// ③ 依赖：<c>AeonglassControlPanel</c>；仅当打的是本角色才挂。
/// ④ ⚠️ 重复挂载只更新；面板销毁随战斗房间。
/// </remarks>

internal static class ControlPanelHost
{
    internal static void Attach(Node parent)
    {
        AeonglassSettingStore.Initialize();

        if (!AeonglassSettingStore.Current.ControlPanel)
        {
            return;
        }

        if (parent.GetNodeOrNull<AeonglassControlPanel>("%" + AeonglassControlPanel.NodeName) is not null)
        {
            return;
        }

        var panel = new AeonglassControlPanel
        {
            Name = AeonglassControlPanel.NodeName,
            UniqueNameInOwner = true,
            ZIndex = 214,
        };

        parent.AddChild(panel);
    }
}
