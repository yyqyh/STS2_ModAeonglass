using Godot;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Presentation.Combat.Gap;
using STS2_Aeonglass.Core.Systems.Sand;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Gap;
namespace STS2_Aeonglass.Core.Presentation.ControlPanel;
/// <summary>间隙页：读数 + 坍缩 / 回归 / 清空（走<b>带演出</b>的入口）。</summary>
/// <remarks>
/// ① 职责：显示间隙与额外手牌状态，提供三个动作按钮。
/// ② 调用时机：页签打开与按钮点击。
/// ③ 依赖：<c>AeonglassGapSequence</c>（带演出）+ <c>GapStore</c>（状态与闸门）。
/// ④ ⚠️ 必须走带演出的入口：不然"演的和做的不一样"。
/// </remarks>

internal sealed partial class GapControlPage : ControlPage
{
    private bool _loggedCombatStart;

    internal override string Title => "间隙";

    internal override bool IsAvailable => GapStore.IsReady;

    protected override void BuildBody(VBoxContainer body)
    {
        var grid = Grid(3);
        // 坍缩 / 回归都走**带演出**的入口（AeonglassGapSequence）：机制 + "卡牌飞进传送门"的动画；
        // 任何一步拿不到（门没建 / 抓不到卡节点）它会自己退回本体表现。
        grid.AddChild(Btn("坍缩 → 间隙", () => Run("坍缩", AeonglassGapSequence.CollapseAsync)));
        grid.AddChild(Btn("回归 → 手牌", () => Run("回归", AeonglassGapSequence.RecallAsync)));
        grid.AddChild(Btn("清空间隙", Clear));

        var more = Grid(1);
        more.AddChild(Btn("打印状态", PrintStatus));

        Text("坍缩 = 复制整手牌进间隙（先清旧内容）；回归 = 丢弃当前手牌、把间隙里的实例搬回手牌。");
    }

    protected override string Status()
    {
        var player = Player();
        if (player is null || !GapStore.IsReady)
        {
            return "不在战斗 / 时空裂隙 未启用";
        }

        // 每场战斗只打一次"进场时间隙里有什么" —— 直接判定"能不能带到下一场战斗"。
        if (!_loggedCombatStart)
        {
            _loggedCombatStart = true;
            Const.Logger.Info($"[{Const.ModId}] [间隙] 进场：{GapStore.Describe(player)}");
        }

        var last = GapAlternation.LastAction(player) switch
        {
            GapAlternation.GapAction.Collapse => "坍缩",
            GapAlternation.GapAction.Recall => "回归",
            _ => "—",
        };
        var sand = SandResourceRegistry.Sand is null
            ? -1
            : SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId);

        return $"间隙 {GapStore.CountOf(player)}/{GapStore.Capacity}"
               + $" ｜ 沙砾 {(sand < 0 ? "?" : sand.ToString())}"
               + $" ｜ 上次动作：{last}（必须交替：同一种不能连做两次）"
               + (AeonglassSettingStore.DebugUnlimited ? " ｜ 调试：不限次" : string.Empty);
    }

    private void Run(string label, Func<Player, Task<GapResult>> action)
    {
        LogPress(label);

        if (Player() is not { } player)
        {
            Show("不在战斗", true);
            return;
        }

        TaskHelper.RunSafely(RunAsync(action, player));
    }

    private async Task RunAsync(Func<Player, Task<GapResult>> action, Player player)
    {
        try
        {
            ShowResult(await action(player));
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] [间隙] 面板动作抛异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            Show($"error: {ex.GetType().Name}: {ex.Message}", true);
        }
    }

    private void Clear()
    {
        LogPress("清空间隙");

        if (Player() is not { } player)
        {
            Show("不在战斗", true);
            return;
        }

        ShowResult(GapStore.Clear(player));
    }

    private void PrintStatus()
    {
        LogPress("打印状态");

        var player = Player();
        var text = player is null ? "不在战斗" : GapStore.Describe(player);
        Const.Logger.Info($"[{Const.ModId}] [间隙] 面板状态：{text}");
        Show(text);
    }

    private void ShowResult(GapResult result) => Show(result.Message, !result.Ok);
}
