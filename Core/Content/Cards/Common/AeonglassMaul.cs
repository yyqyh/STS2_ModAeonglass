using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>重锤</b>（CardBA8）：3 费 · 27 伤（大锤）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassMaul() : AeonglassAttackTemplate(3, CardRarity.Common, 27m, 5m);
