using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 二级资源桥的注册点（本 mod 默认"未接入"占位）。
/// </summary>
/// <remarks>
/// ① 职责：保存/取当前桥实例。
/// ② 调用时机：模块装配与回档抓取。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class SecondaryResourceRuntime
{
    internal static ISecondaryResourceBridge Bridge { get; set; } = NullSecondaryResourceBridge.Instance;
}
