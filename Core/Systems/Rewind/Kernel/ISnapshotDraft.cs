namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>快照草稿：各阶段往这里塞自己的片段，最后合成一个 <see cref="ISnapshot"/>。</summary>
public interface ISnapshotDraft
{
    SnapshotId Id { get; }

    void Put(string key, object? value);

    object? Get(string key);
}
