using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// 滞留 —— 基础 · 1 费 · 技能 · <b>打出后离开牌组，进入「搁浅」；X 场战斗后回到当时的当前卡组</b>。
/// </summary>
/// <remarks>
/// <para>
/// M7「滞留X」的演示 / 验证载体（现在 1 张放在起始卡组里，阶段 ④ 做卡池时再按配比调整）。
/// </para>
/// <list type="bullet">
/// <item><description><b>打出后不进任何原版牌堆</b>：覆写 <c>GetResultLocationForCardPlay</c> 返回 <c>PileType.None</c>，引擎随后自己把它移出战斗；</description></item>
/// <item><description><b>母本离队</b>：在 <c>AfterCardPlayed</c> 里把 <c>DeckVersion</c>（牌组里那张母本）交给搁浅；</description></item>
/// <item><description><b>倒计时随存档</b>：<c>[SavedProperty] CombatsLeft</c>。</description></item>
/// </list>
/// <para>
/// <b>M7 关掉时的降级</b>：这时搁浅牌堆没注册，覆写会退回本体默认去向（进弃牌堆）——
/// 卡退化成一张普通的 0 效果技能牌，<b>不会</b>平白消失。
/// </para>
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

    /// <summary>打出后不回任何原版牌堆（M7 关掉时退回本体默认行为）。</summary>
    protected override CardLocation GetResultLocationForCardPlay()
        => ZonesModule.StasisEnabled && StasisZone.IsReady
            ? new CardLocation(Owner, PileType.None, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    /// <summary>把牌组里的母本交给搁浅；战斗里这张副本由引擎自己移出战斗。</summary>
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

        if (DeckVersion is null)
        {
            Const.Logger.Warn($"[{Const.ModId}] [M7] {Id.Entry} 没有 DeckVersion，无法从牌组移除。");
            return;
        }

        var master = DeckVersion;
        DeckVersion = null;

        await StasisZone.SendToZone(Owner, master, AeonglassSettingStore.Current.StasisCombats);
    }
}
