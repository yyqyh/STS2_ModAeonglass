using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding;

/// <summary>
/// 字段级恢复不会发任何事件，所以恢复完必须补一次刷新：
/// 逐模型触发 <c>InvokeDisplayAmountChanged</c>（刷数字），再通知 StateTracker 一次。
/// </summary>
internal static class UiRefresh
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
