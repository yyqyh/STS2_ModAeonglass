namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>默认实现：内存环（供面板读）+ 转发给宿主 sink。</summary>
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
