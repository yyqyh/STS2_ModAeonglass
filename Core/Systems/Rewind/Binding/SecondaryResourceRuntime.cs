using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding;

/// <summary>
/// 二级资源桥的注册点。本 mod 没有真实资源，默认是"未接入"占位；
/// 宿主接入时替换成基于 RitsuLib <c>SecondaryResourcePersistence</c> 的实现即可。
/// </summary>
internal static class SecondaryResourceRuntime
{
    internal static ISecondaryResourceBridge Bridge { get; set; } = NullSecondaryResourceBridge.Instance;
}
