using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>回摆</b>（CardINV1）：1 费 · 5 挡 · 本回合每翻转一次沙漏额外 +3 挡。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSway() : AeonglassBlockTemplate(1, CardRarity.Common, 5m, 2m)
{
    private const decimal PerInvertBlock = 3m;

    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var inverts = SandTurnCounters.InvertsThisTurn(Owner);
        if (inverts <= 0)
        {
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] 回摆：本回合翻转 {inverts} 次 ⇒ 额外 +{PerInvertBlock * inverts} 挡");
        await CreatureCmd.GainBlock(Owner.Creature, PerInvertBlock * inverts, ValueProp.Move, cardPlay);
    }
}
