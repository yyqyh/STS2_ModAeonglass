using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 切副代价的<b>来源枚举</b>。
/// </summary>
/// <remarks>
/// ① 职责：取值（无 / 沙砾 / 生命 等）。
/// ② 调用时机：设置与结算。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal enum DeckSwitchCostSource
{
    /// <summary>免费（测试期默认）。</summary>
    None = 0,

    /// <summary>沙砾（时之沙）—— "再次动用时间"的第一种解释。</summary>
    Sand = 1,

    /// <summary>能量（战斗内当场付）。</summary>
    Energy = 2,
}
