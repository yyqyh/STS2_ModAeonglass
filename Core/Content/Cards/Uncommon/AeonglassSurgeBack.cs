using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>回涌</b>（新卡 2026-10-19）· <b>1 费 · Uncommon · 技能</b> · <b>迟滞</b> ——
/// 获得 7 点格挡（升级 9）；打出时进入抽牌堆。
/// </summary>
/// <remarks>
/// ① 效果：格挡走模板；落点由 <see cref="GetResultLocationForCardPlay" /> 改成抽牌堆顶。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：<see cref="IHysteresisCard" />（洗牌跳过）+ 本体落点缝 <c>GetResultLocationForCardPlay</c>。
/// ④ ⚠️ "打出时进入抽牌堆"= 不进弃牌堆 ⇒ 它本场只在抽牌堆 ↔ 手牌之间循环；迟滞只是保证
///    万一它进了弃牌堆也不会被随机洗走。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSurgeBack()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Block(1, CardRarity.Uncommon, 7m, 2m)
            .Keyword(HysteresisKeyword.Value)),
    IHysteresisCard
{
    protected override CardLocation GetResultLocationForCardPlay() =>
        new(Owner, PileType.Draw, CardPilePosition.Top);
}
