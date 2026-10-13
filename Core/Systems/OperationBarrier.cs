namespace STS2_Aeonglass.Core.Systems;

/// <summary>
/// <b>操作屏障</b>：我们自己的整段演出（坍缩 / 回归 / 回溯）在跑的时候，把会跟它抢状态的本体操作挡掉。
/// </summary>
/// <remarks>
/// 用户 2026-10-12：「回溯、坍缩等操作都要加屏障」。屏障分两层，职责不同、两层都要有：
/// 本类管"操作层" —— 目前是结束回合（见 <c>Core/Patches/OperationBarrierPatch.cs</c>）；
/// <c>Rewind.Binding.VfxBarrier</c> 管"VFX 层" —— 拦住 M4 回溯，别让它和正在飞的卡节点打架。
/// <para>
/// 只在"我们的角色 + 我们的演出"里为真：进来的一律是模组自己的入口（GapSequence / 回溯模块），
/// 所以原版与其他 mod 的角色永远不会被它挡住。
/// </para>
/// <para>用法：<c>using var scope = OperationBarrier.Enter("M2坍缩");</c> —— 出作用域自动退出。</para>
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
