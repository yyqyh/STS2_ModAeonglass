using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Rewind;
using STS2_Aeonglass.Core.Systems.SandResource;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>穷途陌路</b>（CardRW4，v2 · 原「双线」改名）：3 费 · 消耗所有沙砾，立刻额外回溯一次；
/// 升级：费用 −1（全池唯一减费升级）。<b>搁浅 3</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么带 [搁浅]</b>（用户 2026-10-14）：这张卡的效果是白拿一次回溯。若它还留在牌组里，
/// 每回合都能再来一次，所以用搁浅把它一次性请出牌组，从源头堵掉循环。
/// </para>
/// <para>
/// <b>额外的含义</b>：它不受遗物那条每场 1 次的约束。先 <c>RewindCombatLimit.GrantExtra</c> 拿一份额度，
/// 再走唯一入口 <see cref="RewindModule.TryRequest" />。
/// </para>
/// <para>
/// 回溯代价（成功后消耗全部沙砾）由 <see cref="RewindModule" /> 自己收；这张卡已经先把沙砾花光，
/// 所以那一步实际是 0，顺序不能反。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDeadEnd : ModCardTemplate, IStasisCard
{
    /// <summary>搁浅几场（与铭刻、封沙同口径；用户只说了加搁浅关键词，具体场数待确认）。</summary>
    private const int StrandCombats = 3;

    private int _combatsLeft;

    public AeonglassDeadEnd()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    /// <summary>还剩几次战斗结束才归队（内部按 X + 1 存，所以打出当场那次不计）。</summary>
    [SavedProperty]
    public int CombatsLeft
    {
        get => _combatsLeft;
        set
        {
            AssertMutable();
            _combatsLeft = value;
            DynamicVars["Combats"].BaseValue = Math.Max(0, value);
        }
    }

    /// <summary>卡面显示还需几场战斗归队。</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Combats", StrandCombats)];

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // ① 代价：消耗所有沙砾（M1 的不变式：花掉的沙砾会等量生成空隙）。
        var sand = Math.Max(0, SecondaryResourceCmd.Get(Owner, AeonglassSecondaryResources.SandId));
        if (sand > 0)
        {
            await SecondaryResourceCmd.Spend(Owner, AeonglassSecondaryResources.SandId, sand);
        }

        Const.Logger.Info($"[{Const.ModId}] 穷途陌路：消耗 {sand} 沙砾，立刻额外回溯");

        // ② 额外额度（不受每场 1 次约束）+ 走唯一入口触发回溯。
        RewindCombatLimit.GrantExtra(Owner, 1);
        if (!RewindModule.TryRequest(Owner, out var message))
        {
            Const.Logger.Warn($"[{Const.ModId}] 穷途陌路：回溯没发起（{message}）");
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] 穷途陌路：{message}");
    }

    /// <summary>v2：升级是费用 −1（原版现成 API，本体一堆先例）。</summary>
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);

    /// <summary>打出后不回任何原版牌堆（搁浅没注册时退回本体默认行为）。</summary>
    protected override CardLocation GetResultLocationForCardPlay()
        => ZonesModule.StasisEnabled && StasisZone.IsReady
            ? new CardLocation(Owner, PileType.None, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    /// <summary>把牌组里的母本交给搁浅（战斗里这张副本由引擎自己移出战斗）。</summary>
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
