namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>默认草稿实现。</summary>
public sealed class SnapshotDraft(SnapshotId id) : ISnapshotDraft
{
    private readonly Dictionary<string, object?> _parts = [];

    public SnapshotId Id { get; } = id;

    public IReadOnlyDictionary<string, object?> Parts => _parts;

    public void Put(string key, object? value) => _parts[key] = value;

    public object? Get(string key) => _parts.GetValueOrDefault(key);
}
