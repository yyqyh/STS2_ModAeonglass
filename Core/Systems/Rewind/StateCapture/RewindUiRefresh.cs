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
            }
        }

        Reflect.Invoke(CombatManager.Instance.StateTracker, NotifyCombatStateChanged, "rewind lab restore");
    }
}
