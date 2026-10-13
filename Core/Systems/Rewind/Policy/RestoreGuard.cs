namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// "恢复期"标志（规格 §2.6.3 的恢复期坑 1）。
/// 恢复期间 <c>PowerModel.SetAmount</c> 会被 Harmony prefix 拦掉，改为直接反射写
/// <c>_amount</c> / <c>_amountOnTurnStart</c>，从而一次性覆盖
/// <c>DisplayAmountChanged</c>（NPower 刷数字）与 <c>InvokePowerModified</c>（PowerIncreased/Decreased 事件）两条路径。
/// </summary>
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
