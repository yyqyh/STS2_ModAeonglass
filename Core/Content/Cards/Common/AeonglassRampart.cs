using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary><b>垒壁</b>（CardBB4）：1 费 · 6 挡 · 你的下一张防御牌 +3 点格挡。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassRampart() : AeonglassBlockTemplate(1, CardRarity.Common, 6m, 2m)
{
    private const int NextDefenseBonusAmount = 3;

    /// <summary>加成做成**可见的 buff**（<see cref="AeonglassNextDefensePower" />），不再用隐藏标记。</summary>
    protected override async Task OnPlayExtra(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassNextDefensePower>(
            choiceContext, Owner.Creature, NextDefenseBonusAmount, Owner.Creature, this);
}
