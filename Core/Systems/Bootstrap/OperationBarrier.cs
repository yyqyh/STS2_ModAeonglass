namespace STS2_Aeonglass.Core.Systems.Bootstrap;

/// <summary>
/// 操作屏障：<b>我们自己的整段演出</b>（坍缩/回归/回溯）期间挡住玩家操作。
/// </summary>
/// <remarks>
/// ① 职责：进入/退出成对，期间吞掉输入。
/// ② 调用时机：演出前后。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class OperationBarrier
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, int> Active = [];

    /// <summary>有演出在跑（结束回合这类操作要被挡）。</summary>
    public static bool IsBusy
    {
        get
        {
            lock (Gate)
            {
                return Active.Values.Any(count => count > 0);
            }
        }
    }

    /// <summary>一行摘要（面板 / 日志用）。</summary>
    public static string Describe()
    {
        lock (Gate)
        {
            var busy = Active.Where(pair => pair.Value > 0).Select(pair => pair.Key).ToList();
            return busy.Count == 0 ? "idle" : string.Join(",", busy);
        }
    }

    /// <summary>进屏障；<c>using</c> 出作用域即退出。</summary>
    public static IDisposable Enter(string name) => new Scope(name);

    private static void Add(string name)
    {
        lock (Gate)
        {
            Active[name] = Active.GetValueOrDefault(name) + 1;
        }
    }

    private static void Release(string name)
    {
        lock (Gate)
        {
            if (Active.TryGetValue(name, out var count) && count > 0)
            {
                Active[name] = count - 1;
            }
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly string _name;
        private bool _disposed;

        internal Scope(string name)
        {
            _name = name;
            Add(name);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Release(_name);
        }
    }
}
