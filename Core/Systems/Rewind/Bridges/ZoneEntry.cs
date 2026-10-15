using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Rewind.Bridges;

/// <summary>搁浅在 Rewind 快照里的条目：快照那一刻搁浅区有什么。</summary>
/// <remarks>
/// ① 职责：值对象 —— 母本 <c>NetId</c> + 那张卡 + 剩余战斗数。
/// ② 挂点与时机：由 <c>StasisRewindExtension.OnCapture</c> 造出并塞进 <c>ISnapshotDraft</c>。
/// ③ 依赖：<c>CardModel</c>（只记引用，不做深拷贝）。
/// ④ ⚠️ 仅用于日志与核对；真正的搁浅状态以 <c>StasisStore</c> 的当前内容为准（搁浅不回档）。
/// </remarks>
internal sealed record ZoneEntry(ulong NetId, CardModel Card, int CombatsLeft);
