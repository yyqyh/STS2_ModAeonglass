using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Systems.Sand;

/// <summary>时之沙的核心不变式：<b>花掉多少沙砾就产生多少空隙</b>，并看守 <c>沙砾 + 空隙 = 容量</c>。</summary>
/// <remarks>
/// ① 职责："支付沙砾 → 等量加空隙"的唯一写入口（订阅）+ 不变式自检 <c>CheckInvariant</c> + 转发 <c>ISandSpendListener</c>。
/// ② 挂点与时机：走 RitsuLib 的 <c>SecondaryResourceHook.RegisterGlobalListener</c>（进程级），每次支付时回调、按角色过滤。
/// ③ 依赖：<c>SandResourceRegistry</c> 的两个资源、角色门、<c>ISandSpendListener</c>。
/// ④ ⚠️ 不能写成 <c>CharacterModel</c> 上的 <c>ISecondaryResourceHookListener</c>：本体 <c>IterateHookListeners()</c> 的清单不含角色类，
/// 且 <c>ShouldReceiveCombatHooks == false</c>，当额外模型塞进去也会被过滤 ⇒ 那个方法永远不会被调用（进程级监听效果等价）。
/// 不变式只在"支付"这条路上成立：凭空给沙砾会破坏 <c>沙砾 + 空隙 = 容量</c>，不要再加第二个人为入口。
/// </remarks>
internal sealed class SandGapInvariant : ISecondaryResourceHookListener
{
    /// <summary>防重入：我们自己 <c>Gain</c> 空隙时会再次进来，别把它当成一次"支付"。</summary>
    private bool _dispatching;

    /// <summary>上一次自检时看到的读数（避免同一个坏状态反复刷日志）。</summary>
    private (int Sand, int Void)? _lastChecked;

    /// <summary>沙砾被支付后，产生等量空隙。</summary>
    /// <remarks>
    /// <para>
    /// <b>必须靠 <c>Spend</c> 而不是 <c>Lose</c></b>：按 Lab A 的实测结论，
    /// 支付走不产生 spend 事件的那条路时本回调根本不触发，整条"生成空隙"会丢。
    /// </para>
    /// <para>
    /// 用 <c>context.Amount</c>（本次支付的请求量）。日志把"支付量 / 支付后的沙砾 / 支付前后的空隙"一起打出，
    /// 实机可一眼核对是否与"差值"一致；若将来出现不一致，改读"支付前后 <c>Get</c> 的差值"即可（Lab A 坑 ②）。
    /// </para>
    /// </remarks>
    public async Task AfterSecondaryResourceSpent(SecondaryResourceSpendContext context)
    {
        if (_dispatching)
        {
            return;
        }

        if (context.Definition.Id != SandResourceRegistry.SandId)
        {
            return;
        }

        if (!IsOurs(context.Player))
        {
            return;
        }

        var amount = context.Amount;
        if (amount <= 0)
        {
            return;
        }

        // 回合内计数：这一笔算"本回合消耗过沙砾"（卡面条件用，例：PH-3 双分的消逝副）。
        SandTurnCounters.MarkSandSpent(context.Player, amount);

        // 本场累计消耗（RW-1 反冲的消逝副读它）。
        Systems.History.CombatHistory.RecordSandSpent(context.Player, amount);

        // 消耗通知（SD-7 结壳这类"消耗沙砾触发"的能力读它）。
        await NotifySpendListeners(context.Player, amount);

        var sandBefore = SecondaryResourceCmd.Get(context.Player, SandResourceRegistry.SandId);
        var voidBefore = SecondaryResourceCmd.Get(context.Player, SandResourceRegistry.VoidId);

        _dispatching = true;
        try
        {
            await SecondaryResourceCmd.Gain(
                context.Player,
                SandResourceRegistry.VoidId,
                amount,
                source: null);
        }
        finally
        {
            _dispatching = false;
        }

        var sandAfter = SecondaryResourceCmd.Get(context.Player, SandResourceRegistry.SandId);
        var voidAfter = SecondaryResourceCmd.Get(context.Player, SandResourceRegistry.VoidId);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Sand} 支付 {amount} 沙砾 → 生成空隙"
            + $"（沙砾 {sandBefore}→{sandAfter}，空隙 {voidBefore}→{voidAfter}）");

        CheckInvariant(context.Player, sandAfter, voidAfter);
    }

    /// <summary>
    /// 不变式自检：<c>沙砾 + 空隙 = 容量</c>。
    /// </summary>
    /// <remarks>
    /// 只在读数**变化**且不守恒时打一条警告（同一个坏状态不重复刷），
    /// 用来第一时间抓住"某条路径凭空改了其中一个池子"的回归。
    /// </remarks>
    public void AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (context.Definition.Id != SandResourceRegistry.SandId
            && context.Definition.Id != SandResourceRegistry.VoidId)
        {
            return;
        }

        if (!IsOurs(context.Player))
        {
            return;
        }

        // 回合内记账：这一笔算"本回合沙砾变换过"（翻转 / 消耗 / 获得都过这里）。
        // 例：INV-1 回摆的"沙砾变换过"条件就只读这一个标记。
        if (context.Definition.Id == SandResourceRegistry.SandId && context.Delta != 0)
        {
            SandTurnCounters.MarkSandChanged(context.Player);
        }

        CheckInvariant(
            context.Player,
            SecondaryResourceCmd.Get(context.Player, SandResourceRegistry.SandId),
            SecondaryResourceCmd.Get(context.Player, SandResourceRegistry.VoidId));
    }

    private void CheckInvariant(Player player, int sand, int gap)
    {
        var state = (sand, gap);
        if (_lastChecked == state)
        {
            return;
        }

        _lastChecked = state;

        if (sand + gap == SandResourceRegistry.Capacity)
        {
            return;
        }

        Const.Logger.Warn(
            $"[{Const.ModId}] {Const.Prefix.Sand} 不变式被破坏：沙砾 {sand} + 空隙 {gap} = {sand + gap}"
            + $"，应为 {SandResourceRegistry.Capacity}。"
            + "（检查是不是有入口在凭空增减其中一个池子）");
    }

    /// <summary>是不是我们角色的玩家（次级资源是模组级资源，谁都能持有，所以必须按角色过滤）。</summary>
    private static bool IsOurs(Player? player) => player?.Character is Aeonglass;

    /// <summary>把"消耗了沙砾"转发给该玩家身上实现了 <see cref="ISandSpendListener" /> 的能力。</summary>
    /// <remarks>单个监听者抛异常只记日志，不影响付费本身。</remarks>
    private static async Task NotifySpendListeners(Player player, int amount)
    {
        foreach (var listener in player.Creature.Powers.OfType<ISandSpendListener>().ToList())
        {
            try
            {
                await listener.OnSandSpent(player, amount);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Sand} 消耗通知失败（{listener.GetType().Name}）：{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
