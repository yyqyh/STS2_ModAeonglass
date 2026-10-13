using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 宿主接入点：引擎永不判断"这是哪个角色"，玩法相关逻辑全部走这里。
/// 加一个新机制 = 加一个实现并注册，不改引擎。
/// </summary>
internal interface IRewindExtension
{
    string Name { get; }

    /// <summary>越小的越先执行。</summary>
    int Order { get; }

    /// <summary>拍快照时收集自己的数据。</summary>
    void OnCapture(ISnapshotDraft draft);

    /// <summary>恢复前（例如 M7 先把滞留卡从搁浅摘掉）。</summary>
    void OnBeforeRestore(RewindContext ctx);

    /// <summary>恢复后（例如 M7 母本回牌组 + 清计数）。</summary>
    void OnAfterRestore(RewindContext ctx);
}
