using Godot;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Presentation.UI.ControlPanel;

/// <summary>M1「时之沙」页：两个资源的读数 + 倒置 / 两个验证按钮。</summary>
/// <remarks>
/// <para>
/// 倒置的<b>唯一入口</b>是 <see cref="SandInvertCmd.TryInvert" />（沙砾计数器本体可点也是走它）；
/// 卡牌 / 遗物以后也只调它。
/// </para>
/// <para>
/// 「支付 1 沙砾」与「重置」是<b>验证按钮</b>：必须保持 <c>沙砾 + 空隙 = 容量</c>，
/// 所以只做"走 <c>Spend</c> 的支付"（由订阅补等量空隙）与"整体重置"，
/// <b>不做"凭空 +1 沙砾"</b>（第一版就是因为这个按钮才出现"两个数相加不等于 6"）。
/// </para>
/// </remarks>
internal sealed partial class SandControlPage : ControlPage
{
    internal override string Title => "沙砾";

    internal override bool IsAvailable => AeonglassSecondaryResources.Sand is not null;

    protected override void BuildBody(VBoxContainer body)
    {
        var grid = Grid(3);
        grid.AddChild(Btn($"倒置（{SandInvertCmd.EnergyCost} 费）", () => Run("倒置", p => SandInvertCmd.TryInvert(p))));
        grid.AddChild(Btn("支付 1 沙砾", () => Run("支付 1 沙砾", SpendOneSand)));
        grid.AddChild(Btn("重置 3·3", () => Run("重置 3·3", ResetPools)));

        Text("倒置 = 沙砾 ↔ 空隙 互换（2 费，无每回合次数上限）。");
        Text("「支付 / 重置」是验证按钮，只用来验 `沙砾 + 空隙 = 容量` 这条不变式。");
    }

    protected override string Status()
    {
        var player = Player();
        if (player is null || AeonglassSecondaryResources.Sand is null)
        {
            return "不在战斗 / 资源未注册";
        }

        var sand = SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId);
        var gap = SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.VoidId);
        var capacity = AeonglassSecondaryResources.Capacity;
        var total = sand + gap;
        var invariant = total == capacity ? string.Empty : " ⚠和≠容量";

        return $"沙砾 {sand}/{capacity} ｜ 空隙 {gap}/{capacity} ｜ 和 {total}{invariant}"
               + $" ｜ 能量 {player.PlayerCombatState?.Energy ?? 0}";
    }

    private void Run(string label, Func<Player, Task<bool>> action)
    {
        LogPress(label);

        if (Player() is not { } player)
        {
            Show("不在战斗", true);
            return;
        }

        TaskHelper.RunSafely(RunAsync(action, player));
    }

    private async Task RunAsync(Func<Player, Task<bool>> action, Player player)
    {
        try
        {
            var ok = await action(player);
            Show(ok ? "ok" : "被拒（看日志）", !ok);
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Sand} 面板动作抛异常：{ex.GetType().Name}: {ex.Message}");
            Show($"error: {ex.GetType().Name}: {ex.Message}", true);
        }
    }

    /// <summary>验证用：支付 1 点沙砾（走 <c>Spend</c> ⇒ 由订阅补等量空隙，两个池子的和不变）。</summary>
    private static Task<bool> SpendOneSand(Player player)
        => SecondaryResourceCmd.Spend(player, AeonglassSecondaryResources.SandId, 1);

    /// <summary>验证用：把两个池子重置回初始值（保持总量）。</summary>
    private static async Task<bool> ResetPools(Player player)
    {
        await SecondaryResourceCmd.Set(
            player, AeonglassSecondaryResources.SandId, AeonglassSecondaryResources.InitialSand);
        await SecondaryResourceCmd.Set(
            player, AeonglassSecondaryResources.VoidId, AeonglassSecondaryResources.InitialVoid);
        return true;
    }
}
