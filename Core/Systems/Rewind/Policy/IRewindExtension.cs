using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 回溯的<b>扩展点</b>：玩法侧状态通过它接入抓取/恢复。
/// </summary>
/// <remarks>
/// ① 职责：<c>OnCapture</c> / <c>OnBeforeRestore</c> / <c>OnAfterRestore</c> 三个回调。
/// ② 调用时机：引擎在每个阶段按 order 依次调。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

internal interface IRewindExtension
{
    string Name { get; }

    /// <summary>越小的越先执行。</summary>
    int Order { get; }

    /// <summary>拍快照时收集自己的数据。</summary>
    void OnCapture(ISnapshotDraft draft);

    /// <summary>恢复前（例如 搁浅 先把滞留卡从搁浅摘掉）。</summary>
    void OnBeforeRestore(RewindContext ctx);

    /// <summary>恢复后（例如 搁浅 母本回牌组 + 清计数）。</summary>
    void OnAfterRestore(RewindContext ctx);
}
