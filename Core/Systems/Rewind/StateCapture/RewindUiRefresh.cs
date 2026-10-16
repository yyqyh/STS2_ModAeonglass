using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 字段级恢复不发事件 ⇒ 恢复完必须补一次 UI 刷新。
/// </summary>
/// <remarks>
/// ① 职责：按模型类型派发"显示数值变更/状态变更"。
/// ② 调用时机：回档恢复之后。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class RewindUiRefresh
{
    private const string InvokeDisplayAmountChanged = "InvokeDisplayAmountChanged";

    private const string NotifyCombatStateChanged = "NotifyCombatStateChanged";

    internal static void AfterRestore(IEnumerable<AbstractModel> models)
    {
        foreach (var model in models)
        {
            switch (model)
            {
                case PowerModel power:
                    Reflect.Invoke(power, InvokeDisplayAmountChanged);
                    break;
                case RelicModel relic:
                    Reflect.Invoke(relic, InvokeDisplayAmountChanged);
                    break;
                // ⚠️ 2026-10-16（B4）：卡模型**没有** `InvokeDisplayAmountChanged` 这个入口（本体给卡用的是
                //    `PlayerCombatState.RecalculateCardValues()` + 状态追踪器广播）⇒ 这里不写 card 分支；
                //    卡面刷新由 `CombatStateSnapshot.TryRestore` 第 7.5 步的重算 + 下面这次"状态变了"广播负责。
            }
        }

        Reflect.Invoke(CombatManager.Instance.StateTracker, NotifyCombatStateChanged, "rewind lab restore");
    }
}
