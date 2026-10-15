using Godot;

using STS2_Aeonglass.Core.Systems.Rewind;
using STS2_Aeonglass.Core.Systems.Rewind.Host;

namespace STS2_Aeonglass.Core.Presentation.ControlPanel;
/// <summary>回溯页：本场额度 + 手动回溯验证。</summary>
/// <remarks>
/// ① 职责：显示本场是否还能回溯，提供一次回溯按钮。
/// ② 调用时机：页签打开与按钮点击。
/// ③ 依赖：<c>RewindModule</c>（唯一入口）+ <c>RewindCombatLimit</c>。
/// ④ ⚠️ 走 <c>RequestFromQueuedAction</c>（排队动作链路），直调会被闸门拦。
/// </remarks>

internal sealed partial class RewindControlPage : ControlPage
{
    internal override string Title => "回溯";

    internal override bool IsAvailable => true;

    protected override void BuildBody(VBoxContainer body)
    {
        var grid = Grid(2);
        grid.AddChild(Btn("回溯 → 上一回合", Request));
        grid.AddChild(Btn("打印闸门", PrintGates));

        Text("回溯 = 回到本回合开始（每场 1 次 + 成功后消耗全部沙砾）；敌人时间不回退，只被污染。");
    }

    protected override string Status()
    {
        var player = Player();
        return player is null ? "不在战斗" : RewindModule.Describe(player);
    }

    private void Request()
    {
        LogPress("回溯");

        if (Player() is not { } player)
        {
            Show("不在战斗", true);
            return;
        }

        try
        {
            if (!RewindModule.TryRequest(player, out var message))
            {
                Show(message, true);
                return;
            }

            Show(message);
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Rewind} 面板动作抛异常：{ex.GetType().Name}: {ex.Message}");
            Show($"error: {ex.GetType().Name}: {ex.Message}", true);
        }
    }

    private void PrintGates()
    {
        LogPress("打印闸门");

        var gate = RewindFacade.Instance.EvaluateGates();
        var text = string.Join(" | ", gate.Checks.Select(c => c.Display));
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 闸门：{text}");
        Show(gate.Ok ? "闸门全过（可回溯）" : $"被拦：{gate.FirstFailureName}（{gate.FirstFailureReason}）", !gate.Ok);
    }
}
