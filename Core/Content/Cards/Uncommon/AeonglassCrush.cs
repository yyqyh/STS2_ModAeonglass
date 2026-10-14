using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>碾碎</b>（CardBA10）：2 费 · 18 伤（升级 +5）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassCrush() : AeonglassAttackTemplate(2, CardRarity.Uncommon, 18m, 5m);
