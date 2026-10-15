namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>
/// 「这张卡要花几点沙砾」的**声明式入口**。
/// </summary>
/// <remarks>
/// <para>
/// 卡自己在构造函数里用 <c>SecondaryCosts().Set(...)</c> 声明费用；本接口只是把这个数字**再说一遍**，
/// 好让 间隙的"换回补挂"（<see cref="GapCopyRestore" />）能**按规范声明**重新挂上 ——
/// 因为 <c>ToSerializable()</c> 会丢掉 RitsuLib 的次级资源费用（定价表 §0 的已知语义）。
/// </para>
/// <para>纪律：带沙砾费的卡都实现它，且实现值必须与构造函数里 <c>Set</c> 的一致。</para>
/// </remarks>
internal interface ISandCostCard
{
    /// <summary>打出这张卡要消耗的沙砾数量（0 = 免费）。</summary>
    int SandCost { get; }
}
