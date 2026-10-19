using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// <b>防御</b>（起始基础牌 ×4）· <b>1 费 · Basic · 技能</b> · <c>CardTag.Defend</c> —— 获得 5 点格挡（升级 8）。
/// </summary>
/// <remarks>
/// ① 效果：一次普通格挡；升级 +3 挡。
/// ② 差分：无差分（两副共用，起始各 4 张）。
/// ③ 依赖：起始牌组注册（×4）；<c>CardTag.Defend</c> 让"你的下一张防御牌 +N"（「防护」）认它。
/// ④ ⚠️ 卡面走 RitsuLib 的内嵌占位图，不要求美术；数值按 v2 施工。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
[RegisterCharacterStarterCard(typeof(Aeonglass), 4)]
public sealed class AeonglassDefend()
    : AeonglassCardTemplate(AeonglassCardSpec.Block(1, CardRarity.Basic, 5m, 3m))
{
}
