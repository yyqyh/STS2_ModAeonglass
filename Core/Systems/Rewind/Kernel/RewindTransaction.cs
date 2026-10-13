namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>一次回档的最终结果。</summary>
public readonly record struct RewindOutcome(bool Ok, bool RolledBack, string Error);

/// <summary>
/// 回档事务：加锁 → 等运行期稳定 → 备份当前状态 → 恢复 → 校验 → 失败回滚 → 解锁。
/// 内核不认识游戏，只依赖注入的锁 / 校验器 / 帧等待。
/// </summary>
public sealed class RewindTransaction(
    IRewindLock rewindLock,
    IRewindValidator validator,
    IRewindLog log,
    Func<bool> isStable,
    Func<Task> waitFrame,
    int maxWaitFrames = 240)
{
    public async Task<RewindOutcome> RunAsync(ISnapshot target, Func<ISnapshot?> backupFactory)
    {
        rewindLock.Engage(target.Id.ToString());

        try
        {
            if (!await WaitForStableAsync())
            {
                return new RewindOutcome(false, false, "运行期未稳定，已放弃回档");
            }

            // 备份"当前状态"：恢复中途失败时用它退回来。
            var backup = SafeBackup(backupFactory);

            if (!target.TryRestore(log, out var restoreError))
            {
                return Rollback(backup, $"恢复失败：{restoreError}");
            }

            if (!validator.Validate(out var invalidReason))
            {
                log.Error($"回档后不变量校验失败：{invalidReason}");
                return Rollback(backup, $"校验失败：{invalidReason}");
            }

            log.Info($"rewind ok → {target.Id}");
            return new RewindOutcome(true, false, string.Empty);
        }
        catch (Exception ex)
        {
            log.Error($"回档异常：{ex.GetType().Name}: {ex.Message}");
            return new RewindOutcome(false, false, ex.Message);
        }
        finally
        {
            rewindLock.Release();
        }
    }

    private async Task<bool> WaitForStableAsync()
    {
        for (var frame = 0; frame <= maxWaitFrames; frame++)
        {
            if (isStable())
            {
                return true;
            }

            if (frame == maxWaitFrames)
            {
                break;
            }

            await waitFrame();
        }

        log.Warn($"等待 {maxWaitFrames} 帧后运行期仍未稳定");
        return false;
    }

    private ISnapshot? SafeBackup(Func<ISnapshot?> factory)
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            log.Warn($"备份快照失败（将不带回滚能力继续）：{ex.GetType().Name}");
            return null;
        }
    }

    private RewindOutcome Rollback(ISnapshot? backup, string reason)
    {
        if (backup is null)
        {
            log.Error($"{reason}；无备份快照，无法回滚");
            return new RewindOutcome(false, false, reason);
        }

        if (backup.TryRestore(log, out var rollbackError))
        {
            log.Warn($"{reason}；已回滚到回档前状态");
            return new RewindOutcome(false, true, reason);
        }

        log.Error($"{reason}；回滚也失败了：{rollbackError}");
        return new RewindOutcome(false, false, $"{reason} / 回滚失败：{rollbackError}");
    }
}
