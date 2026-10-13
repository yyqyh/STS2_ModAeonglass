using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>砂袭</b>（CardBA7）：0 费 · 6 伤（0 费地板）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSandRush() : AeonglassAttackTemplate(0, CardRarity.Common, 6m, 2m);
