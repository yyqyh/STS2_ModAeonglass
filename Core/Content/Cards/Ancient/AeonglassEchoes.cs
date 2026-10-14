using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Ancient;

/// <summary>
/// <b>遗响</b>（AN-1，先古）：3 费 · 40 伤害 · <b>搁浅 4</b>（搁浅线的终点：四场离队的一次性重击）。
/// </summary>
/// <remarks>先古牌没有升级档（卡表：升级不变）。</remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassEchoes() : AeonglassAttackTemplate(3, CardRarity.Ancient, 40m, 0m), IStasisCard
{
    private const int StrandCombats = 4;

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
