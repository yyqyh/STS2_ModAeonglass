using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>劈砍</b>（CardBA1）：1 费 · 8 伤。底线件·无条件。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCleave() : AeonglassAttackTemplate(1, CardRarity.Common, 8m, 3m);
