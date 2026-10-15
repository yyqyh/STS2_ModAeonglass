using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.History;
using STS2_Aeonglass.Core.Systems.Rewind;

namespace STS2_Aeonglass.Core.Content.Relics;

/// <summary>
/// 初始遗物：<b>永恒沙漏</b> —— 装有时之沙、同时服务[永时]/[消逝]两副卡组，<b>右键切换卡组</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>它也是"选人界面必须存在的那件遗物"</b>：本体选人界面里那句写死的
/// <c>RelicModel relicModel = characterModel.StartingRelics[0];</c>
/// （见 <c>MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen.SelectCharacter</c>）——
/// <b>角色必须有至少一件初始遗物，否则点它会抛 <c>ArgumentOutOfRangeException</c>，
/// 选人失败、游戏沿用上一个角色开局</b>（2026-10-11 实测踩到：表现为"选了沙漏，进去却是铁甲战士"）。
/// </para>
/// <para>
/// <b>2026-10-14 用户定档</b>：它是<b>双卡组的右键入口</b>（永时 ⇄ 消逝），
/// 而"回溯"的右键入口搬到事件遗物「破碎的怀表」（<see cref="AeonglassBrokenWatch" />）。
/// 沙砾本身是 run 级资源（M1 次级资源），这里的"沙漏装有时之沙"是它的叙事归属。
/// </para>
/// <para>
/// <b>美术</b>：指向 <c>images/relics/aeonglass_core.png</c>（刻意做成透明的空槽图）——
/// 顶栏与选人界面上它现在是"看不见的"，出图后同名覆盖即可。
/// </para>
/// </remarks>
[RegisterRelic(typeof(AeonglassRelicPool))]
public sealed class AeonglassCore : ModRelicTemplate, IModRightClickableRelic, IRelicExtraIconAmountLabelSpecsProvider
{
    /// <summary>初始遗物。</summary>
    public override RelicRarity Rarity => RelicRarity.Starter;

    // ============================ 战斗历史打点（ANN / RW 通道的卡读它）============================
    // 起始遗物是天然的战斗 hook 监听者（每个 run 都在场）⇒ 借它把"打出的牌 / 回合结束"喂给历史层，
    // 不用为此再造一个隐形内容。

    /// <summary>记下"谁打出了什么牌"（历史层用）。</summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner is { } player)
        {
            CombatHistory.RecordCardPlayed(player, cardPlay.Card);
        }

        return Task.CompletedTask;
    }

    /// <summary>一方回合结束 ⇒ 该方"本回合读数"滚成"上回合"。</summary>
    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Combat.CombatSide side,
        IEnumerable<Creature> participants)
    {
        foreach (var creature in participants)
        {
            if (creature.Player is { } player)
            {
                CombatHistory.EndTurn(player);
            }
        }

        return Task.CompletedTask;
    }

    // ============================ 「迟滞」的执行点（规则层 v1.1 新关键词）============================
    // 本体的洗牌（弃牌堆 → 抽牌堆）走 `Hook.ModifyShuffleOrder` → 逐个 hook 监听者调
    // `AbstractModel.ModifyShuffleOrder(player, cards, isInitialShuffle)`。
    // 遗物是天然的战斗 hook 监听者，而且这是**每个 run 都在场**的初始遗物 ⇒ 借它执行"迟滞"，
    // 不用为这个关键词再造一个隐形内容。

    /// <summary>
    /// 「迟滞」：带标记的牌**不参与洗牌**（留在弃牌堆）；全是迟滞牌时忽略它一次（无死锁兜底）。
    /// </summary>
    public override void ModifyShuffleOrder(Player player, List<CardModel> cards, bool isInitialShuffle)
    {
        base.ModifyShuffleOrder(player, cards, isInitialShuffle);

        if (cards.Count == 0)
        {
            return;
        }

        var stuck = cards.Where(Hysteresis.IsStuck).ToList();
        if (stuck.Count == 0)
        {
            return;
        }

        // 兜底：整批都是迟滞牌 ⇒ 这一次忽略迟滞（否则抽牌堆会是空的 = 死锁）。
        if (stuck.Count == cards.Count)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] 迟滞：本次要洗的 {stuck.Count} 张全是迟滞牌 ⇒ 忽略迟滞以避免死锁");
            return;
        }

        foreach (var card in stuck)
        {
            cards.Remove(card);
        }

        Const.Logger.Info(
            $"[{Const.ModId}] 迟滞：{stuck.Count} 张不参与本次洗牌（留在弃牌堆）："
            + string.Join(", ", stuck.Select(card => card.Id.Entry)));
    }

    /// <summary>图标 / 描边 / 大图都指向同一张空槽图。</summary>
    public override RelicAssetProfile AssetProfile => new(
        IconPath: Const.Paths.Relic.Core,
        IconOutlinePath: Const.Paths.Relic.Core,
        BigIconPath: Const.Paths.Relic.Core);

    // ============================ M6 双卡组的右键入口（2026-10-14 用户定档）============================
    // 走 RitsuLib 的右键遗物体系（`IModRightClickableRelic`）—— 它把右键打包成**同步动作**，
    // 所以将来支持多人时这条入口天然安全（不像"本地直调"那样会只在本机换状态）。
    // 回溯的右键入口已搬到事件遗物「破碎的怀表」（AeonglassBrokenWatch）。

    /// <summary>本地快速筛选：只认我们自己的遗物 + 本角色（其余情况不拦）。</summary>
    public bool CanHandleRightClickLocal(ModRightClickContext context) =>
        context.Model is AeonglassCore && context.Player.Character is Aeonglass;

    /// <summary>同步判定：本角色 + M6 就绪（更细的闸门交给 <see cref="DoubleDeckService.ManualSwitchAsync" /> 回话）。</summary>
    public bool CanExecuteRightClick(ModRightClickExecutionContext context) =>
        context.Player.Character is Aeonglass && DecksModule.Enabled && DoubleDeckService.IsReady;

    /// <summary>右键 = <b>切换卡组</b>（永时 ⇄ 消逝）。</summary>
    /// <remarks>
    /// <para>
    /// 走的还是 M6 自己那条**主动切换**入口（<see cref="DoubleDeckService.ManualSwitchAsync" />）⇒
    /// 代价、每场上限、冷却这些闸门全都照旧生效，这里只负责"把右键接上去"。
    /// </para>
    /// <para>
    /// 延迟一小拍再发起：右键是**排队动作**，立刻切换会跟引擎自己的动作队列抢时序
    /// （回溯入口当初也踩过同一个坑，见 <see cref="RewindModule.RequestFromQueuedAction" />）。
    /// </para>
    /// </remarks>
    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        var chain = context.Action is null ? "本地直调（多人下会不同步！）" : "同步动作";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 永恒沙漏右键：切换卡组（链路={chain}）");
        TaskHelper.RunSafely(SwitchDeckSoonAsync(context.Player));
        return Task.CompletedTask;
    }

    private static async Task SwitchDeckSoonAsync(Player player)
    {
        try
        {
            await Cmd.Wait(SwitchDelay, ignoreCombatEnd: true);
            var outcome = await DoubleDeckService.ManualSwitchAsync(player, null);
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 永恒沙漏右键切换：{(outcome.Ok ? "成功" : "被拒")}｜{outcome.Message}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] {Const.Prefix.Decks} 右键切换卡组抛异常：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>切换前等多久（秒）—— 只为错开"右键还在动作队列里"的那一小段。</summary>
    private const float SwitchDelay = 0.2f;

    // ============================ 遗物额外角标（用户 2026-10-11 要求，x/2 形态）============================
    // 走 RitsuLib 的「额外角标」体系（教程 04-22-1）：`IRelicExtraIconAmountLabelSpecsProvider`。
    // 角落：TopLeft / TopRight / BottomLeft / BottomRight（BottomRight 是原版主计数，别占）。

    /// <summary>
    /// 一个角标：<b>右上 = 切换冷却</b>（<c>已积累/需要</c>，就是用户说的 <c>x/2</c> 那种形态）。
    /// </summary>
    /// <remarks>
    /// 数值来源在机制层（<c>DeckState</c>），这里只做显示；
    /// 拿不到玩家（未入手 / 已移除）时返回空列表。
    /// 回溯的 x/1 角标已随入口搬到「破碎的怀表」。
    /// </remarks>
    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        if (Owner is not { } player)
        {
            return [];
        }

        var cooldown = Math.Clamp(AeonglassSettingStore.Current.DecksManualCooldownCombats, 0, 99);
        var since = DeckState.CombatsSinceManualSwitch(player);
        var switchText = cooldown > 0 ? $"{since}/{cooldown}" : since.ToString();

        return
        [
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.TopRight,
                $"[color=gold]{switchText}[/color]"),
        ];
    }

    // ============================ M6 双卡组的状态载体 ============================
    // 为什么挂在遗物上：本体的"局内可存字段"只认 [SavedProperty]，而角色模型（CharacterModel）不在
    // 钩子分发里、也不随局内数据走（堕化拉撒路的实测结论）。遗物是这局真正持有的对象，最合适。
    // 注意：这里只放**状态**，入口在 Core/Systems/Decks/（M6 的机制代码），遗物本身不写机制逻辑。

    /// <summary>M6：当前生效的卡组（0 = A，1 = B）。</summary>
    /// <remarks>
    /// "生效中的那副"永远真实地躺在 <c>player.Deck</c> 里；另一副存在 <c>deck_b</c> 这个
    /// <c>RunPersistent</c> 牌堆里（活实例常驻）。这个索引只回答"哪一副现在生效"。
    /// </remarks>
    [SavedProperty]
    public int ActiveDeckIndex { get; set; }

    /// <summary>M6：非生效那一副是否已经初始化过（只播种一次）。</summary>
    [SavedProperty]
    public bool DecksInitialized { get; set; }

    /// <summary>M6：距上次"主动切换"已经过了几场战斗（主动切换的冷却用）。</summary>
    [SavedProperty]
    public int CombatsSinceManualSwitch { get; set; }

    /// <summary>
    /// M4：<b>本局（跨战斗）累计回溯次数</b> —— 「因果」的加成读它。
    /// </summary>
    /// <remarks>
    /// 和 M6 那几个状态同一个理由放在遗物上：本体的"局内可存字段"只认 <c>[SavedProperty]</c>，
    /// 而遗物是这局真正持有、随存档走的对象 ⇒ 跨战斗、跨读档都在。
    /// <b>只存状态</b>：写入点在 <c>RunHistory</c>（机制层），遗物自己不写机制逻辑。
    /// </remarks>
    [SavedProperty]
    public int RewindsThisRun { get; set; }

    /// <summary>
    /// 「破碎的怀表」那件事**还没办完**（M4 的事件链用；随存档走）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 记在遗物上是因为它要<b>跨读档</b>：在初始房间存档 / 读档之后，那个先古事件房间是"已完成"状态恢复的，
    /// 本体**不会再触发** <c>EventRoom.OnEventStateChanged</c> ⇒ 光靠那个钩子会漏掉"读档"这条路
    /// （用户 2026-10-14 实测：初始房间 SL 之后事件不再出现）。
    /// </para>
    /// <para>拿到怀表（或在别的入口把它发出去）之后由机制层清零。</para>
    /// </remarks>
    [SavedProperty]
    public bool BrokenWatchPending { get; set; }

}
