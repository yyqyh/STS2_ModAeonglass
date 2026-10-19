using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>惯性</b>（CardINV3，v2）· <b>1 费 · Uncommon · 能力</b> —— 每当你翻转沙漏：抽 2 张牌（每回合 1 次）；升级不变。
/// </summary>
/// <remarks>
/// ① 效果：打出即挂 <c>AeonglassInertiaPower</c>（点数 2 = 每次抽几张）；升级不抬数值。
/// ② 差分：无差分。
/// ③ 依赖：翻转钩子（与 时空裂隙 激活共用计时）+ "每回合 1 次"的回合内计数。
/// ④ ⚠️ 升级档不变 ⇒ 价格上等于"没有升级收益"（卡池侧口径）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassInertia() : AeonglassCardTemplate(
    AeonglassCardSpec.Power(1, CardRarity.Uncommon)
        .Var("OnInvertDraw", OnInvertDrawBase)
        .Effect(CardEffects.ApplyPower<AeonglassInertiaPower>("OnInvertDraw")))
{
    private const decimal OnInvertDrawBase = 2m;
}
