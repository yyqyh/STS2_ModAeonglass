using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Stasis;
namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// <b>滞留</b>（搁浅 演示载体 · Basic，<b>当前不在起始牌组里</b>）· <b>1 费 · Basic · 技能</b> —— 打出后离开牌组、进入「搁浅」；X 场战斗后回到<b>当时的当前卡组</b>。
/// </summary>
/// <remarks>
/// ① 效果：打出后不回任何原版牌堆（落点 <c>PileType.None</c>），把牌组里的母本交给搁浅区；倒计时随存档走。
/// ② 差分：无差分。
/// ③ 依赖：搁浅区（<c>ExtraPileModules</c> / <c>StasisStore</c>）+ <c>[SavedProperty] CombatsLeft</c>；
///    与「铭刻 / 封沙 / 破片」共用同一套搁浅流程（那三张已收进 <c>AeonglassStasis*Template</c>）。
/// ④ ⚠️ 搁浅未注册（<c>ExtraPileModules.StasisEnabled = false</c>）时退回本体默认落点（进弃牌堆）—— 卡退化成 0 效果技能牌，<b>不会</b>平白消失。
///    起始牌组现在是 打击×4 + 防御×4 + 潮汐×1 + 堤岸×1（共 10 张，见 <c>DeckBSeed</c>）⇒ 本卡要靠奖励/控制台拿到。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassStasis : ModCardTemplate, IStasisCard
{
    private int _combatsLeft;

    public AeonglassStasis()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    /// <summary>还剩几次"战斗结束"才归队（内部按 <c>X + 1</c> 存，所以打出当场那次不计）。</summary>
    [SavedProperty]
    public int CombatsLeft
    {
        get => _combatsLeft;
        set
        {
            AssertMutable();
            _combatsLeft = value;

            // Step 3：卡面显示**剩余场数**（而不是初始 X）。
            // 照本体 Dowsing 的写法：把计数写回动态变量，牌堆界面 / 手牌渲染时就会取到最新值。
            DynamicVars["Combats"].BaseValue = Math.Max(0, value);
        }
    }

    /// <summary>卡面上显示「还需几场战斗归队」（<b>剩余场数</b>；Step 3 起随倒计时刷新）。</summary>
    /// <remarks>
    /// 初值取设置里的 X；实例化 / 读档后由 <see cref="CombatsLeft" /> 的 setter 覆盖成真实剩余值。
    /// </remarks>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Combats", Math.Max(0, AeonglassSettingStore.Current.StasisCombats))];

    protected override HashSet<CardTag> CanonicalTags => [];

    /// <summary>打出后不回任何原版牌堆（搁浅 关掉时退回本体默认行为）。</summary>
    protected override CardLocation GetResultLocationForCardPlay()
        => ExtraPileModules.StasisEnabled && StasisStore.IsReady
            ? new CardLocation(Owner, PileType.None, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    /// <summary>把牌组里的母本交给搁浅；战斗里这张副本由引擎自己移出战斗。</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, this) || Owner is null)
        {
            return;
        }

        if (!ExtraPileModules.StasisEnabled || !StasisStore.IsReady)
        {
            return;
        }

        if (DeckVersion is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [搁浅] {Id.Entry} 没有 DeckVersion，无法从牌组移除。");
            return;
        }

        var master = DeckVersion;
        DeckVersion = null;

        await StasisReturn.SendToZone(Owner, master, AeonglassSettingStore.Current.StasisCombats);
    }
}
