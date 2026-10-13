using Godot;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Presentation.UI.ControlPanel;

/// <summary>把统一控制面板挂进战斗场景（随房间销毁）。</summary>
/// <remarks>
/// 2026-10-13：原来是 6 个 Host（M1/M2/M3/M4/M6 面板 + 外观面板）各自判断开关，现在只剩这一个。
/// 关掉 <c>AeonglassSetting.ControlPanel</c> ⇒ 什么都不做：不生成节点、不注册输入。
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
