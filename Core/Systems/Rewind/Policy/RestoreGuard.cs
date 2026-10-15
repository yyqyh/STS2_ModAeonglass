namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 回溯的<b>恢复期标志</b>：恢复期间钩子要放行（别把回档当成玩家操作）。
/// </summary>
/// <remarks>
/// ① 职责：<c>Enter()</c> 返回可释放作用域，恢复期判定。
/// ② 调用时机：恢复流程与相关补丁/钩子。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

internal static class RestoreGuard
{
    private static int _depth;

    internal static bool IsRestoring => _depth > 0;

    /// <summary>进入恢复期；返回一个 IDisposable，离开时自动退出（异常安全）。</summary>
    internal static IDisposable Enter()
    {
        _depth++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_depth > 0)
            {
                _depth--;
            }
        }
    }
}
