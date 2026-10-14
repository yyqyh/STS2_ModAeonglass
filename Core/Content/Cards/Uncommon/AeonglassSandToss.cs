using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary><b>抛沙</b>（CardSTA1）：1 费 · 12 伤（+3）· <b>搁浅 2</b>（打出后离队，2 场战斗后归队）。</summary>
/// <remarks>
/// 搁浅时机与实现照基础卡「滞留」：打出后把<b>牌组里的母本</b>交给搁浅区（<c>StasisZone.SendToZone</c>），
/// 战斗里那张副本由引擎自己移出战斗；离场期间不参与牌组。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSandToss() : AeonglassAttackTemplate(1, CardRarity.Uncommon, 12m, 3m), IStasisCard
{
    /// <summary>本卡固定的搁浅场数（卡表口径，不是设置里的 X）。</summary>
    private const int StrandCombats = 2;

    private int _combatsLeft;

    /// <summary>还剩几次"战斗结束"才归队（内部按 X + 1 存，打出当场那次不计）。</summary>
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

    /// <summary>把牌组里的母本交给搁浅。</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, this) || Owner is null)
        {
            return;
        }

        if (!ZonesModule.StasisEnabled || !StasisZone.IsReady)
        {
            return;
        }

        if (DeckVersion is not { } master)
        {
            Const.Logger.Warn($"[{Const.ModId}] [M7] {Id.Entry} 没有 DeckVersion，无法从牌组移除。");
            return;
        }

        DeckVersion = null;
        await StasisZone.SendToZone(Owner, master, StrandCombats);
    }
}
