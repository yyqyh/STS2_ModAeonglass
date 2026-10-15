namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>快照草稿</b>接口：扩展往里塞自己那一份数据。
/// </summary>
/// <remarks>
/// ① 职责：按 key 存取草稿片段。
/// ② 调用时机：<c>IRewindExtension.OnCapture</c>。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public interface ISnapshotDraft
{
    SnapshotId Id { get; }

    void Put(string key, object? value);

    object? Get(string key);
}
