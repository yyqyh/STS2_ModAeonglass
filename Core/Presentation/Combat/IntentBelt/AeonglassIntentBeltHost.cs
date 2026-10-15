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

        // 2026-10-15 用户口径：调度器**挂到敌侧容器**（`%EnemyContainer`）—— 语义归位（它就是"给敌侧挂卡带的人"）。
        // ⚠️ `%EnemyContainer` 会被本体重设 Scale（= 相机缩放）与 Position，所以**别把它的子节点当屏幕空间用**：
        // 卡带本体并不挂在这下面，而是挂在每只怪的 `NCreature/IntentContainer` 同级（自己做了缩放补偿）。
        // 找不到敌侧容器就退回原来的父节点（房间根）——调度器不画东西，挂哪儿都一样跑。
        var enemyContainer = parent.GetNodeOrNull<Control>("%EnemyContainer");
        var host = enemyContainer is not null && GodotObject.IsInstanceValid(enemyContainer) ? enemyContainer : parent;

        var manager = new AeonglassIntentBeltManager
        {
            Name = AeonglassIntentBeltManager.NodeName,
            UniqueNameInOwner = true,
            ZIndex = 0,
        };

        host.AddChild(manager);
    }
}
