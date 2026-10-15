namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>快照环</b>：按容量保存最近 N 个快照（回合粒度的历史）。
/// </summary>
/// <remarks>
/// ① 职责：入环 / 取上一个 / 清理与容量控制。
/// ② 调用时机：每回合开始抓一次；回溯时取上一个。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public sealed class SnapshotRing(int capacity = 5)
{
    private readonly List<ISnapshot> _items = [];
    private int _nextIndex;

    public int Capacity { get; } = Math.Max(1, capacity);

    /// <summary>被淘汰时触发（UI 刷新用）。</summary>
    public event Action? Changed;

    public IReadOnlyList<ISnapshot> Items => _items;

    public ISnapshot? Latest => _items.Count == 0 ? null : _items[^1];

    public SnapshotId NextId(int turnNumber, string label) =>
        new(_nextIndex, turnNumber, DateTime.Now, label);

    public ISnapshot Add(ISnapshot snapshot)
    {
        _nextIndex++;
        _items.Add(snapshot);
        while (_items.Count > Capacity)
        {
            var evicted = _items[0];
            _items.RemoveAt(0);
            try
            {
                evicted.Release();
            }
            catch
            {
                // 淘汰失败不影响其它快照
            }
        }

        Changed?.Invoke();
        return snapshot;
    }

    public ISnapshot? ByIndex(int index) => _items.FirstOrDefault(s => s.Id.Index == index);

    /// <summary>
    /// 回档默认目标：**当前回合之前最近的一份快照**（即"上一回合开始"）。
    /// 未来要放开"任意快照点回档"，从这里换实现即可，UI 不用改。
    /// </summary>
    public ISnapshot? PreviousTurn(int currentTurnNumber) =>
        _items.LastOrDefault(s => s.Id.TurnNumber < currentTurnNumber);

    public void Clear()
    {
        foreach (var item in _items)
        {
            try
            {
                item.Release();
            }
            catch
            {
                // 忽略
            }
        }

        _items.Clear();
        Changed?.Invoke();
    }
}
