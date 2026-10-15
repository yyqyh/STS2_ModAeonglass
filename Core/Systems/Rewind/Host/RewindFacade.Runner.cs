using Godot;

using STS2_Aeonglass.Core.Systems;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture.Guards;
using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Rewind.Host;

/// <summary>
/// 回溯的<b>执行侧</b>：把一次回溯从请求跑到恢复完成（含帧等待与回滚保护）。
/// </summary>
/// <remarks>
/// ① 职责：<c>RequestRewindToPreviousTurn</c> / <c>UndoLastRewind</c> / <c>RestoreToSnapshotAsync</c> / <c>RunRewindAsync</c>。
/// ② 调用时机：<c>RewindFacade</c> 转发（右键遗物、控制面板、卡牌效果都经门面）。
/// ③ 依赖：<c>RewindFacade</c>（引擎与快照环）+ 外锁/屏障。
/// ④ ⚠️ 恢复期间要挡玩家输入、暂停队列；失败必须把外锁放掉。
/// </remarks>
internal sealed partial class RewindFacade
{

    /// <summary>
    /// 请求回档到"上一回合开始"。走事务：加锁 → 等稳定 → 备份 → 恢复 → 校验 → 失败回滚。
    /// 面板/热键调用这个（内部异步，不阻塞 UI 帧）。
    /// </summary>
    /// <param name="onFinished">
    /// 回档**结束**后的回调（参数 = 是否成功）。宿主用它做"成功后收取代价"这类收尾 ——
    /// 不能在调用点之前收代价：时之沙的沙砾会被快照恢复回去，代价就白收了。
    /// </param>
    public void RequestRewindToPreviousTurn(Action<bool>? onFinished = null)
    {
        if (RewindInProgress)
        {
            Log.Warn("已有回档正在进行，忽略本次请求");
            onFinished?.Invoke(false);
            return;
        }

        var gate = EvaluateGates();
        if (!gate.Ok)
        {
            LastResult = $"回档被拦截：{gate.FirstFailureName}（{gate.FirstFailureReason}）";
            Log.Warn($"rewind blocked: {LastResult}");
            onFinished?.Invoke(false);
            return;
        }

        var target = Ring.PreviousTurn(CombatAccess.CurrentTurnNumber());
        if (target is null)
        {
            LastResult = "回档失败：没有可回退的上一回合快照";
            Log.Warn("rewind blocked: no previous-turn snapshot");
            onFinished?.Invoke(false);
            return;
        }

        RewindInProgress = true;
        _ = RunRewindAsync(target, onFinished);
    }

    /// <summary>撤销上一次成功回档（回溯 只记录状态，实际撤销留给后续里程碑）。</summary>
    public bool UndoLastRewind(out string error)
    {
        if (!_lastRewindSucceeded)
        {
            error = "本次战斗还没有成功回档过";
            return false;
        }

        error = "撤销回档尚未接入";
        return false;
    }

    /// <summary>
    /// 自检 / 面板专用：直接回档到指定快照（不走"上一回合"解析、不额外检查闸门，但仍走完整事务）。
    /// 备份快照不进环形缓冲，也<b>不</b>调用扩展的 <c>OnCapture</c>（避免覆盖目标快照的扩展片段）。
    /// </summary>
    public async Task<bool> RestoreToSnapshotAsync(ISnapshot target)
    {
        if (RewindInProgress)
        {
            Log.Warn("已有回档正在进行，忽略 RestoreToSnapshotAsync");
            return false;
        }

        var turn = CombatAccess.CurrentTurnNumber();
        RewindInProgress = true;

        // 回溯也进"操作屏障"：这段时间里点结束回合会被拒（用户 2026-10-12：「回溯、坍缩等操作都要加屏障」）。
        using var barrier = OperationBarrier.Enter("回溯");

        try
        {
            var outcome = await _transaction.RunAsync(
                target,
                () => CombatStateSnapshot.Capture(Ring.NextId(turn, "backup"), Scope));

            _lastRewindSucceeded = outcome.Ok;
            LastResult = outcome.Ok
                ? $"回档 {target.Id} 成功（直达）"
                : outcome.RolledBack
                    ? $"回档失败并已回滚：{outcome.Error}"
                    : $"回档失败：{outcome.Error}";
            return outcome.Ok;
        }
        catch (Exception ex)
        {
            _lastRewindSucceeded = false;
            LastResult = $"回档异常：{ex.GetType().Name}: {ex.Message}";
            Log.Error(LastResult);
            return false;
        }
        finally
        {
            RewindInProgress = false;
        }
    }

    private async Task RunRewindAsync(ISnapshot target, Action<bool>? onFinished = null)
    {
        var turn = CombatAccess.CurrentTurnNumber();
        var ok = false;

        // 同上：回溯全程进操作屏障。
        using var barrier = OperationBarrier.Enter("回溯");

        try
        {
            var outcome = await _transaction.RunAsync(
                target,
                () => CombatStateSnapshot.Capture(Ring.NextId(turn, "backup"), Scope));

            if (outcome.Ok)
            {
                _lastRewindSucceeded = true;
                State.ToIdle($"已回档到 {target.Id}");
                LastResult = $"回档 #{target.Id.Index}（T{target.Id.TurnNumber}）成功";
                ok = true;
                return;
            }

            _lastRewindSucceeded = false;
            State.ToFailed(outcome.Error);
            LastResult = outcome.RolledBack ? $"回档失败并已回滚：{outcome.Error}" : $"回档失败：{outcome.Error}";
        }
        catch (Exception ex)
        {
            _lastRewindSucceeded = false;
            State.ToFailed(ex.Message);
            LastResult = $"回档异常：{ex.GetType().Name}: {ex.Message}";
            Log.Error(LastResult);
        }
        finally
        {
            RewindInProgress = false;
            onFinished?.Invoke(ok);
        }
    }

    /// <summary>等一帧（事务里等运行期稳定用）。</summary>
    internal static async Task WaitFrameAsync()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            await tree.ToSignal(tree, "process_frame");
            return;
        }

        await Task.Delay(16);
    }
}
