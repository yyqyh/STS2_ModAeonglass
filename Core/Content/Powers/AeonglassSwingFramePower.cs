using STS2RitsuLib.Interop.AutoRegistration;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「摆幅」（GRW-3 挂的能力）：你的手牌上限 +<c>Amount</c>（间隙容量那一半另挂「深隙」）。</summary>
[RegisterPower]
public sealed class AeonglassSwingFramePower : AeonglassHandLimitPower;
