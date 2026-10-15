namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>日志抽象</b>：内核不认识游戏 Logger，由宿主注入。
/// </summary>
/// <remarks>
/// ① 职责：<c>IRewindLog</c>（接口）+ <c>RewindLog</c>（适配到本 mod 的日志）。
/// ② 调用时机：引擎全程。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public sealed class RewindLog(int capacity = 200, Action<string>? sink = null) : IRewindLog
{
    private readonly Queue<string> _tail = new();

    public IReadOnlyList<string> Tail => _tail.ToArray();

    public void Info(string message) => Append("info", message);

    public void Warn(string message) => Append("warn", message);

    public void Error(string message) => Append("error", message);

    private void Append(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
        _tail.Enqueue(line);
        while (_tail.Count > capacity)
        {
            _tail.Dequeue();
        }

        sink?.Invoke(line);
    }
}
