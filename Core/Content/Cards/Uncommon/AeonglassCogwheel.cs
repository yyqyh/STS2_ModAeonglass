using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>齿车</b>（CardUTL4，v2）· <b>1 费 · Uncommon · 能力</b> —— 每当你打出一张 <b>0 费牌</b>：获得 1 点能量（每回合 1 次）；升级不变。
/// </summary>
/// <remarks>
/// ① 效果：打出即挂 <c>AeonglassCogwheelPower</c>（点数 1 = 每次回多少能量）；每回合只触发 1 次，回合开始重置。
/// ② 差分：无差分。
/// ③ 依赖：能力层 <c>AeonglassCogwheelPower</c>（打出的卡 <c>EnergyCost.Canonical == 0</c> 判定 + 回合开始的 `_usedThisTurn` 复位）。
/// ④ ⚠️ 判 0 费读的是**卡面基础费用**（Canonical），不是被减费后的实际费用 —— 与本体"0 费牌"类效果的通常口径一致。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCogwheel() : AeonglassCardTemplate(
    AeonglassCardSpec.Power(1, CardRarity.Uncommon)
        .Effect(CardEffects.ApplyPower<AeonglassCogwheelPower>()));
