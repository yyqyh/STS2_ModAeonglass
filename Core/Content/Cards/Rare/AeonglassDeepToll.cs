using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>沉钟</b>（CardDLY2）：1 费 · 13 伤（+3）· <b>迟滞</b>。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDeepToll() : AeonglassAttackTemplate(1, CardRarity.Rare, 13m, 3m), IHysteresisCard;
