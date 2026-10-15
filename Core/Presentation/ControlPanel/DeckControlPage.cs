using Godot;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Presentation.ControlPanel;
/// <summary>卡组页：双副读数 / 切换 / 孪生镜像自检。</summary>
/// <remarks>
/// ① 职责：显示当前生效副、非生效副是否播种，提供切换与镜像自检。
/// ② 调用时机：页签打开与按钮点击。
/// ③ 依赖：<c>DeckPiles</c> / <c>DeckTwins</c>。
/// ④ ⚠️ 切换走服务层入口（右键切副同一入口），别直接改牌堆。
/// </remarks>

internal sealed partial class DeckControlPage : ControlPage
{
    internal override string Title => "卡组";

    internal override bool IsAvailable => DeckModule.IsReady;

    protected override void BuildBody(VBoxContainer body)
    {
        var grid = Grid(2);
        grid.AddChild(Btn("切换卡组（热切换）", SwitchDeck));
        grid.AddChild(Btn("打印状态", PrintStatus));

        Section("迟滞（测试）");
        var stuck = Grid(3);
        stuck.AddChild(Btn("手牌第 1 张打迟滞", MarkFirstHandCard));
        stuck.AddChild(Btn("清空迟滞标记", () =>
        {
            LogPress("清空迟滞标记");
            Hysteresis.ClearMarks();
            Show(Hysteresis.Describe());
        }));
        stuck.AddChild(Btn("打印迟滞", () =>
        {
            LogPress("打印迟滞");
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} {Hysteresis.Describe()}");
            Show(Hysteresis.Describe());
        }));

        Text("热切换 = 换主卡组 + 战斗内牌堆重铺 + 补抽一手（走 BlockingPlayerChoiceContext）。");
        Text("代价与冷却（沙砾 / 每场次数 / 每 2 场 1 次）在设置页里调。");
        Text("迟滞：带标记的牌不参与洗牌（留在弃牌堆）；整批都是迟滞时忽略它一次以免死锁。");

        Section("两副基础牌：孪生镜像（测试）");
        var twins = Grid(2);
        twins.AddChild(Btn("孪生自检（当前副的首张打击 +1）", TwinSelfCheck));
        twins.AddChild(Btn("打印孪生", () =>
        {
            LogPress("打印孪生");
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} {DeckTwins.Describe()}");
            Show(DeckTwins.Describe());
        }));
        Text("孪生 = 同一张基础牌在两副各一份实例；改一处的升级等级 → 另一处自动跟上（单一 choke point 挂在 CurrentUpgradeLevel 的 setter 上）。");
    }

    /// <summary>测试用：把当前生效副的首张「打击」升 1 级，看另一副的孪生有没有跟上。</summary>
    private void TwinSelfCheck()
    {
        LogPress("孪生自检");

        if (Player() is not { } player)
        {
            Show("不在战斗", true);
            return;
        }

        var strike = player.Deck.Cards.FirstOrDefault(card => card.Id.Entry.Contains("STRIKE", StringComparison.OrdinalIgnoreCase));
        if (strike is null)
        {
            Show("当前副里找不到「打击」（换了语言 / 改名就会这样）", true);
            return;
        }

        if (strike.CurrentUpgradeLevel >= strike.MaxUpgradeLevel)
        {
            Show($"「{strike.Id.Entry}」已经满级 +{strike.CurrentUpgradeLevel}；改成看孪生：{DescribeTwin(strike)}");
            return;
        }

        // 走本体自己的升级入口（孪生镜像就挂它的 postfix 上）——这样这条自检才真的验证了 choke point。
        strike.UpgradeInternal();
        Show($"「{strike.Id.Entry}」→ +{strike.CurrentUpgradeLevel}｜孪生 {DescribeTwin(strike)}");
    }

    private static string DescribeTwin(CardModel card)
        => DeckTwins.TwinOf(card) is not { } twin
            ? "未配对（播种时没找到同名牌）"
            : $"{twin.Id.Entry} +{twin.CurrentUpgradeLevel}";

    /// <summary>测试用：给手牌第一张打上「迟滞」（洗牌时它就该留在弃牌堆）。</summary>
    private void MarkFirstHandCard()
    {
        LogPress("手牌第 1 张打迟滞");

        if (Player() is not { } player)
        {
            Show("不在战斗", true);
            return;
        }

        var card = player.PlayerCombatState?.Hand.Cards.FirstOrDefault();
        if (card is null)
        {
            Show("手牌是空的", true);
            return;
        }

        Hysteresis.Mark(card);
        Show($"{card.Id.Entry} 已打迟滞（共 {Hysteresis.Count} 张）");
    }

    protected override string Status()
    {
        var player = Player();
        return player is null ? "不在战斗" : DeckModule.Describe(player);
    }

    private void SwitchDeck()
    {
        LogPress("切换卡组");

        if (Player() is not { } player)
        {
            Show("不在战斗", true);
            return;
        }

        // 热切换要补抽一手，需要一个出牌上下文；面板没有天然上下文，用阻塞式那个。
        TaskHelper.RunSafely(SwitchAsync(player, new BlockingPlayerChoiceContext()));
    }

    private async Task SwitchAsync(Player player, PlayerChoiceContext context)
    {
        try
        {
            var outcome = await DeckSwitchService.ManualSwitchAsync(player, context);
            Show(outcome.Message, !outcome.Ok);
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Decks} 面板动作抛异常：{ex.GetType().Name}: {ex.Message}");
            Show($"error: {ex.GetType().Name}: {ex.Message}", true);
        }
    }

    private void PrintStatus()
    {
        LogPress("打印状态");

        var text = Player() is { } player ? DeckModule.Describe(player) : "不在战斗";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 面板状态：{text}");
        Show(text);
    }
}
