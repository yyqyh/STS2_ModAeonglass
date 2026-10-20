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
/// <b>双源取牌</b>（NEW-32，2026-10-20）· <b>1 费 · Uncommon · 能力</b> · <b>迟滞</b> ——
/// 回合开始时不再从抽牌堆抽 5 张，而是从抽牌堆抽 3 张、从弃牌堆抽 2 张（升级：弃牌堆抽 3）。
/// </summary>
/// <remarks>
/// ① 效果：<c>ModifyHandDraw</c> 从常规抽牌里扣掉 N，再由能力在回合开始补抽 N 张弃牌堆。
/// ② 这是"常规抽牌替换"，不是额外抽牌；弃牌堆不足时按本体兜底（有多少抽多少）。
/// ③ 依赖：<c>AeonglassDoubleSourcePower</c> + <c>DiscardDraw</c>。
/// ④ ⚠️ 其他遗物/能力对"手牌抽取数"的修正仍会先作用在 5 这个基数上，再被本能力扣 N。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDoubleSource()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Power(1, CardRarity.Uncommon)
            .Keyword(HysteresisKeyword.Value)
            .Var("FromDiscard", 2m, upgradeDelta: 1m)
            .Effect(CardEffects.ApplyPower<AeonglassDoubleSourcePower>("FromDiscard"))),
    IHysteresisCard;
