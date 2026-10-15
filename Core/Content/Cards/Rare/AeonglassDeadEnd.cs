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
using STS2_Aeonglass.Core.Systems.Sand;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Stasis;
namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>穷途陌路</b>（CardRW4，v2 · 原「双线」改名）· <b>3 费 · Rare · 技能</b> —— 消耗所有[沙砾]，立刻额外[回溯]一次；<b>搁浅 3</b>。升级：<b>费用 −1</b>。
/// </summary>
/// <remarks>
/// ① 效果：先花光沙砾 → 拿一份额外回溯额度 → 走唯一入口发起回溯 → 打出后交给搁浅（3 场战斗后归队）；升级让这张牌自己 −1 费。
/// ② 差分：无差分。
/// ③ 依赖：时之沙 沙砾（<c>SecondaryResourceCmd</c>）+ 回溯（<c>RewindCombatLimit.GrantExtra</c> / <c>RewindModule.TryRequest</c>）+ 搁浅区（<c>ExtraPileModules</c> / <c>StasisStore</c>）。
/// ④ ⚠️ 顺序不能反：回溯的代价是"消耗全部沙砾"，本卡已经把沙砾花光 ⇒ 那一步实际是 0。
///    带搁浅是<b>故意的</b>（用户 2026-10-14）：效果等于白拿一次回溯，留在牌组里会变成每回合循环 ⇒ 用搁浅一次性请出牌组堵掉。
///    本卡是全池唯一"技能 + 搁浅"的卡，仍手写搁浅那段样板（模板目前只有攻击/格挡两味）。
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
        // ① 代价：消耗所有沙砾（时之沙的不变式：花掉的沙砾会等量生成空隙）。
        var sand = Math.Max(0, SecondaryResourceCmd.Get(Owner, SandResourceRegistry.SandId));
        if (sand > 0)
        {
            await SecondaryResourceCmd.Spend(Owner, SandResourceRegistry.SandId, sand);
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
        => ExtraPileModules.StasisEnabled && StasisStore.IsReady
            ? new CardLocation(Owner, PileType.None, CardPilePosition.Bottom)
            : base.GetResultLocationForCardPlay();

    /// <summary>把牌组里的母本交给搁浅（战斗里这张副本由引擎自己移出战斗）。</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, this) || Owner is null)
        {
            return;
        }

        if (!ExtraPileModules.StasisEnabled || !StasisStore.IsReady || DeckVersion is not { } master)
        {
            return;
        }

        DeckVersion = null;
        await StasisReturn.SendToZone(Owner, master, StrandCombats);
    }
}
