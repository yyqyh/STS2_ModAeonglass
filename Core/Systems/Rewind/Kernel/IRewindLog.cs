namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>引擎日志抽象：内核不认识游戏的 Logger，由宿主注入。</summary>
public interface IRewindLog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message);

    /// <summary>最近若干条（旧 → 新），供面板显示。</summary>
    IReadOnlyList<string> Tail { get; }
}
