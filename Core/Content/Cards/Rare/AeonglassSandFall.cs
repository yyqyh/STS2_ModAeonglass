using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>倒沙</b>（CardDLY1）：0 费 · 9 伤（+3）· <b>迟滞</b>（不参与洗牌，留在弃牌堆）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSandFall() : AeonglassAttackTemplate(0, CardRarity.Rare, 9m, 3m), IHysteresisCard;
