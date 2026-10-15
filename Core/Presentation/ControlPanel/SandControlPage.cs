using Godot;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Presentation.ControlPanel;
/// <summary>沙砾页：读数 + 倒置 / 给沙砾 / 花沙砾的验证按钮。</summary>
/// <remarks>
/// ① 职责：显示沙砾与空隙，提供三个资源操作的验证入口。
/// ② 调用时机：页签打开与按钮点击。
/// ③ 依赖：次级资源命令（时之沙）+ 计数器 UI。
/// ④ ⚠️ 直接改资源会触发 时之沙 订阅（<b>花沙砾必生成等量空隙</b>），验证时留意两个读数一起变。
/// </remarks>

internal sealed partial class SandControlPage : ControlPage
{
    internal override string Title => "沙砾";

    internal override bool IsAvailable => SandResourceRegistry.Sand is not null;

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
        if (player is null || SandResourceRegistry.Sand is null)
        {
            return "不在战斗 / 资源未注册";
        }

        var sand = SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId);
        var gap = SecondaryResourceCmd.Get(player, SandResourceRegistry.VoidId);
        var capacity = SandResourceRegistry.Capacity;
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
        => SecondaryResourceCmd.Spend(player, SandResourceRegistry.SandId, 1);

    /// <summary>验证用：把两个池子重置回初始值（保持总量）。</summary>
    private static async Task<bool> ResetPools(Player player)
    {
        await SecondaryResourceCmd.Set(
            player, SandResourceRegistry.SandId, SandResourceRegistry.InitialSand);
        await SecondaryResourceCmd.Set(
            player, SandResourceRegistry.VoidId, SandResourceRegistry.InitialVoid);
        return true;
    }
}
