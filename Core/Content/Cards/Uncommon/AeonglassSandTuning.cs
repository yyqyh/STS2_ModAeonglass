using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>调沙</b>（新卡 2026-10-19）· <b>1 费 · Uncommon · 能力</b> · <b>迟滞</b> ——
/// 回合结束时选择一张[迟滞]牌置于抽牌堆顶。升级：费用 −1。
/// </summary>
/// <remarks>
/// ① 效果：挂 <see cref="AeonglassSandTuningPower" />；真正的"回合结束选牌"在能力里。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：<see cref="IHysteresisCard" /> + 能力回合结束钩子 + 手牌选牌界面。
/// ④ ⚠️ 需要选牌界面 ⇒ 主路径实机验；手牌里没有迟滞牌时能力静默跳过（不弹空界面）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSandTuning()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Power(1, CardRarity.Uncommon)
            .Keyword(HysteresisKeyword.Value)
            .UpgradeEnergy(-1)
            .Effect(CardEffects.ApplyPower<AeonglassSandTuningPower>())),
    IHysteresisCard;
