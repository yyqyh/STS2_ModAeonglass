namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>「这张卡要花几点沙砾」的<b>声明式入口</b>。</summary>
/// <remarks>
/// ① 职责：声明 <c>SandCost</c>，供换回补挂（<see cref="GapCopyRestore" />）按规范重新挂费用。
/// ② 挂点与时机：带沙砾费的卡在构造函数里 <c>SecondaryCosts().Set(...)</c>，本接口把同一个数字再说一遍。
/// ③ 依赖：无。
/// ④ ⚠️ 纪律：带沙砾费的卡都实现它，返回值必须与构造函数里 <c>Set</c> 的值一致 ——
/// 因为 <c>ToSerializable()</c> 会丢掉 RitsuLib 的次级资源费用（定价表 §0 的已知语义）。
/// </remarks>
internal interface ISandCostCard
{
    /// <summary>打出这张卡要消耗的沙砾数量（0 = 免费）。</summary>
    int SandCost { get; }
}
