using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 回档期的<b>外锁</b>：禁玩家操作 → 停 UI → 暂停执行器与玩家队列。
/// </summary>
/// <remarks>
/// ① 职责：<c>IRewindLock</c> 的实现（进入/退出成对）。
/// ② 调用时机：恢复流程前后；异常路径必须放锁。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal sealed class CombatRewindLock(IRewindLog log) : IRewindLock
{
    public void Engage(string reason)
    {
        Guard(() => Reflect.SetProperty(CombatManager.Instance, "PlayerActionsDisabled", true), "disable player actions");
        Guard(() => NCombatRoom.Instance?.Ui?.Disable(), "disable combat ui");
        Guard(() => RunManager.Instance.ActionExecutor.Pause(), "pause action executor");
        Guard(() => RunManager.Instance.ActionQueueSet.PauseAllPlayerQueues(), "pause player queues");
        log.Info($"rewind lock engaged ({reason})");
    }

    public void Release()
    {
        Guard(() => RunManager.Instance.ActionQueueSet.UnpauseAllPlayerQueues(), "unpause player queues");
        Guard(() => RunManager.Instance.ActionExecutor.Unpause(), "unpause action executor");
        Guard(() => Reflect.SetProperty(CombatManager.Instance, "PlayerActionsDisabled", false), "enable player actions");
        Guard(() => NCombatRoom.Instance?.Ui?.Enable(), "enable combat ui");
        log.Info("rewind lock released");
    }

    private void Guard(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            log.Warn($"{what} failed: {ex.GetType().Name}");
        }
    }
}
