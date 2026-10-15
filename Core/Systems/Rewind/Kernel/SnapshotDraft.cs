namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>快照草稿的默认实现：内存里的一张 key → 片段表。</summary>
/// <remarks>
/// ① 职责：实现 <c>ISnapshotDraft</c> 的 <c>Put</c> / <c>Get</c>。
/// ② 挂点与时机：引擎拍快照时 <c>new SnapshotDraft(id)</c>，交给各 <c>IRewindExtension.OnCapture</c> 填。
/// ③ 依赖：<c>SnapshotId</c>、<c>ISnapshotDraft</c>。
/// ④ ⚠️ 只活在内存里（随快照环一起丢弃），不做序列化、不跨会话。
/// </remarks>

public sealed class SnapshotDraft(SnapshotId id) : ISnapshotDraft
{
    private readonly Dictionary<string, object?> _parts = [];

    public SnapshotId Id { get; } = id;

    public IReadOnlyDictionary<string, object?> Parts => _parts;

    public void Put(string key, object? value) => _parts[key] = value;

    public object? Get(string key) => _parts.GetValueOrDefault(key);
}
