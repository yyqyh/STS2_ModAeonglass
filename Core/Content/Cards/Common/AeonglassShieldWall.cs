using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>盾墙</b>（CardBB1）：1 费 · 6 挡（格挡基准）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassShieldWall() : AeonglassBlockTemplate(1, CardRarity.Common, 6m, 3m);
