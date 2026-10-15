namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>引擎日志抽象：内核不认识游戏的 Logger，由宿主注入。</summary>
/// <remarks>
/// ① 职责：<c>Info</c> / <c>Warn</c> / <c>Error</c> + <c>Tail</c>（最近若干条，供面板显示）。
/// ② 挂点与时机：内核在抓拍 / 恢复的关键节点写。
/// ③ 依赖：无（实现方是 <c>RewindLog</c>）。
/// ④ ⚠️ 只给引擎用；业务日志仍走 <c>Const.Logger</c>。
/// </remarks>
public interface IRewindLog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message);

    /// <summary>最近若干条（旧 → 新），供面板显示。</summary>
    IReadOnlyList<string> Tail { get; }
}
