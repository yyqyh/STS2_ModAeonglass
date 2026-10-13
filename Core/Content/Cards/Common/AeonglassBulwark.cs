using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>壁垒</b>（CardBB2）：2 费 · 11 挡。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassBulwark() : AeonglassBlockTemplate(2, CardRarity.Common, 11m, 3m);
