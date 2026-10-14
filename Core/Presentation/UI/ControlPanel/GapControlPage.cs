using Godot;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Presentation.Visuals;
using STS2_Aeonglass.Core.Systems.SandResource;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Presentation.UI.ControlPanel;

/// <summary>M2「时空间隙」页：间隙读数 + 坍缩（带演出）/ 回归（带演出）/ 清空。</summary>
/// <remarks>
/// <para>
/// 按用户口径：牌堆按键只负责"看"（点开原版牌堆界面），<b>坍缩 / 回归</b>做成公开接口
/// （<see cref="TimeGap.Activate" /> / <see cref="TimeGap.Recall" />）给后续卡牌 / 机制调用，
/// 这个面板是它们现在的<b>临时</b>入口。
/// </para>
/// <para>
/// 状态行里的「上次动作」就是 M2 唯一那道速率闸（必须交替）的实时读数 ——
/// 「每回合 1 次激活」的上限已按用户 2026-10-14 口径取消。
/// </para>
/// </remarks>
internal sealed partial class GapControlPage : ControlPage
{
    private bool _loggedCombatStart;

    internal override string Title => "间隙";

    internal override bool IsAvailable => TimeGap.IsReady;

    protected override void BuildBody(VBoxContainer body)
    {
        var grid = Grid(3);
        // 坍缩 / 回归都走**带演出**的入口（GapSequence）：机制 + "卡牌飞进传送门"的动画；
        // 任何一步拿不到（门没建 / 抓不到卡节点）它会自己退回本体表现。
        grid.AddChild(Btn("坍缩 → 间隙", () => Run("坍缩", GapSequence.CollapseAsync)));
        grid.AddChild(Btn("回归 → 手牌", () => Run("回归", GapSequence.RecallAsync)));
        grid.AddChild(Btn("清空间隙", Clear));

        var more = Grid(1);
        more.AddChild(Btn("打印状态", PrintStatus));

        Text("坍缩 = 复制整手牌进间隙（先清旧内容）；回归 = 丢弃当前手牌、把间隙里的实例搬回手牌。");
    }

    protected override string Status()
    {
        var player = Player();
        if (player is null || !TimeGap.IsReady)
        {
            return "不在战斗 / M2 未启用";
        }

        // 每场战斗只打一次"进场时间隙里有什么" —— 直接判定"能不能带到下一场战斗"。
        if (!_loggedCombatStart)
        {
            _loggedCombatStart = true;
            Const.Logger.Info($"[{Const.ModId}] [M2] 进场：{TimeGap.Describe(player)}");
        }

        var last = GapTurnLimit.LastAction(player) switch
        {
            GapTurnLimit.GapAction.Collapse => "坍缩",
            GapTurnLimit.GapAction.Recall => "回归",
            _ => "—",
        };
        var sand = AeonglassSecondaryResources.Sand is null
            ? -1
            : SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId);

        return $"间隙 {TimeGap.CountOf(player)}/{TimeGap.Capacity}"
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
                $"[{Const.ModId}] [M2] 面板动作抛异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
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

        ShowResult(TimeGap.Clear(player));
    }

    private void PrintStatus()
    {
        LogPress("打印状态");

        var player = Player();
        var text = player is null ? "不在战斗" : TimeGap.Describe(player);
        Const.Logger.Info($"[{Const.ModId}] [M2] 面板状态：{text}");
        Show(text);
    }

    private void ShowResult(GapResult result) => Show(result.Message, !result.Ok);
}
