using STS2RitsuLib.Interop.AutoRegistration;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「容器」（GRW-4 挂的能力）：你的手牌上限 +<c>Amount</c>（演出用的抽牌与格挡在卡上）。</summary>
[RegisterPower]
public sealed class AeonglassVesselPower : AeonglassHandLimitPower;
