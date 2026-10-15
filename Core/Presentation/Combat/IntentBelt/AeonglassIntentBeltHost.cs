using Godot;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
/// <summary>把意图卡带挂给战斗里"被托管的怪"。</summary>
/// <remarks>
/// ① 职责：遍历被托管的怪，逐个 <c>Ensure</c> 卡带（幂等）。
/// ② 调用时机：<c>CombatUiMountPatch</c> 战斗房间 <c>_Ready</c>。
/// ③ 依赖：<c>AeonglassIntentBeltManager</c> / 意图（哪些怪被托管）。
/// ④ ⚠️ 重复挂只更新、不重建；怪死/换房间由 Manager 清理。
/// </remarks>

internal static class AeonglassIntentBeltHost
{
    internal static void Attach(Node parent)
    {
        AeonglassSettingStore.Initialize();

        var setting = AeonglassSettingStore.Current;
        if (!setting.IntentEnabled || !setting.IntentIntentBelt)
        {
            return;
        }

        if (parent.GetNodeOrNull<AeonglassIntentBeltManager>("%" + AeonglassIntentBeltManager.NodeName) is not null)
        {
            return;
        }

        var manager = new AeonglassIntentBeltManager
        {
            Name = AeonglassIntentBeltManager.NodeName,
            UniqueNameInOwner = true,
            ZIndex = 215,
        };

        parent.AddChild(manager);
    }
}
