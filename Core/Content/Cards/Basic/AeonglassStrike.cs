using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// <b>打击</b>（起始基础牌 ×4）· <b>1 费 · Basic · 攻击</b> · <c>CardTag.Strike</c> —— 造成 6 点伤害（升级 9）。
/// </summary>
/// <remarks>
/// ① 效果：一次普通攻击；升级 +3 伤。
/// ② 差分：无差分（两副共用，起始各 4 张）。
/// ③ 依赖：起始牌组注册（<c>[RegisterCharacterStarterCard(..., 4)]</c>）；本体 <c>DamageCmd.Attack</c> 的 trigger 固定为 <c>"Attack"</c>，由沙漏的状态机映射到 <c>attack_heavy</c> 动画。
/// ④ 卡面：直接使用 <c>images/cards/aeonglass_strike.png</c>（已就位）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
[RegisterCharacterStarterCard(typeof(Aeonglass), 4)]
public sealed class AeonglassStrike()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Attack(1, CardRarity.Basic, 6m, 3m)
            .Tag(CardTag.Strike))
{
    public override CardAssetProfile AssetProfile =>
        new(PortraitPath: "res://STS2_Aeonglass/images/cards/aeonglass_strike.png");
}
