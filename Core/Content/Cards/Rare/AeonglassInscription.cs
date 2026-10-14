using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary><b>铭刻</b>（CardSTA3）：3 费 · 27 伤（+6）· <b>搁浅 3</b>。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassInscription() : AeonglassAttackTemplate(3, CardRarity.Rare, 27m, 6m), IStasisCard
{
    private const int StrandCombats = 3;

    private int _combatsLeft;

    /// <summary>还剩几次"战斗结束"才归队（内部按 X + 1 存）。</summary>
    [SavedProperty]
    public int CombatsLeft
    {
        get => _combatsLeft;
        set => _combatsLeft = value;
    }

    /// <summary>打出后不回任何原版牌堆（搁浅没注册时退回本体默认行为）。</summary>
    protected override CardLocation GetResultLocationForCardPlay()
        => ZonesModule.StasisEnabled && StasisZone.IsReady
            ? new CardLocation(Owner, PileType.None, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, this) || Owner is null)
        {
            return;
        }

        if (!ZonesModule.StasisEnabled || !StasisZone.IsReady || DeckVersion is not { } master)
        {
            return;
        }

        DeckVersion = null;
        await StasisZone.SendToZone(Owner, master, StrandCombats);
    }
}
