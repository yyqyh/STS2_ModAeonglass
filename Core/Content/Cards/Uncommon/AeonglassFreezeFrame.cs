using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>定格</b>（CardDLY3）：0 费 · 9 挡（升级 +3）· <b>迟滞</b>（不参与洗牌，留在弃牌堆）。</summary>
/// <remarks>
/// 迟滞的机制判定走标记接口 <see cref="IHysteresisCard" />（mod 关键词共用 None 槽位 ⇒ 没法按槽位判，
/// 见该接口注释）；词条显示暂时只能靠卡面文字写「迟滞」。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassFreezeFrame() : AeonglassBlockTemplate(0, CardRarity.Uncommon, 9m, 3m),
    IHysteresisCard;
