using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding;

/// <summary>
/// 回档期的外锁：禁玩家操作 → 停 UI → 暂停执行器 → 暂停所有玩家队列。
/// 解锁按相反顺序；每一步都不允许抛（失败只记日志）。
/// </summary>
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
