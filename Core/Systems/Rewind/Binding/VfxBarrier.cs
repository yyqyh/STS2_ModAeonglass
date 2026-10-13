namespace STS2_Aeonglass.Core.Systems.Rewind.Binding;

/// <summary>
/// 异步卡牌 VFX 的"在飞"计数器。
/// 这些动画是不 await 的后台任务，结束后还会继续改/释放卡节点；回档期间重建手牌就会撞上它们。
/// 严格模式下一次有 VFX 在飞就拒绝回档（见 <c>RewindScope.VfxBarrierStrict</c>）。
/// </summary>
internal static class VfxBarrier
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, int> Active = [];

    internal static bool IsBusy
    {
        get
        {
            lock (Gate)
            {
                return Active.Values.Any(count => count > 0);
            }
        }
    }

    internal static string Describe()
    {
        lock (Gate)
        {
            var busy = Active.Where(pair => pair.Value > 0).Select(pair => $"{pair.Key}={pair.Value}").ToList();
            return busy.Count == 0 ? "idle" : string.Join(",", busy);
        }
    }

    /// <summary>包一层：原任务真正结束（含其后的清理）才计数减一。</summary>
    internal static async Task Track(string name, Task task)
    {
        Enter(name);
        try
        {
            await task;
        }
        catch
        {
            // 动画失败不影响回档判断
        }
        finally
        {
            Exit(name);
        }
    }

    private static void Enter(string name)
    {
        lock (Gate)
        {
            Active[name] = Active.GetValueOrDefault(name) + 1;
        }
    }

    private static void Exit(string name)
    {
        lock (Gate)
        {
            if (Active.TryGetValue(name, out var count) && count > 0)
            {
                Active[name] = count - 1;
            }
        }
    }
}
