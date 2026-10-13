using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Systems.SandResource;

/// <summary>
/// M1「时之沙」的核心不变式：<b>花掉多少沙砾，就产生多少空隙</b>；并顺带看守
/// <c>沙砾 + 空隙 = 容量</c>。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么它是"角色类的那份订阅"却不是一个模型钩子</b>：RitsuLib 的次级资源钩子是从本体
/// <c>CombatState.IterateHookListeners()</c> 收集的，而那串清单只有
/// <b>能力 / 怪物 / 遗物 / 药水 / 充能球 / 战斗牌堆里的牌（含侵蚀与附魔）/ 修正器 / 徽章 / 联机缩放</b>
/// —— <b>不含 <c>CharacterModel</c></b>；而且 <c>CharacterModel.ShouldReceiveCombatHooks == false</c>，
/// 连"当额外模型塞进去"那条路也会被过滤掉。
/// 所以把 <c>ISecondaryResourceHookListener</c> 直接写在 <c>Aeonglass</c> 类上，那个方法<b>永远不会被调用</b>。
/// </para>
/// <para>
/// 因此走 RitsuLib 的第三条路：<see cref="SecondaryResourceHook.RegisterGlobalListener" /> 注册
/// <b>进程级监听器</b>，每个回调按角色过滤 —— 效果与"挂在角色类上"完全等价。
/// </para>
/// <para>
/// <b>不变式只在"支付"这条路上成立</b>：沙砾的其它来源不产生空隙，空隙的减少（M2 的换回代价）也不回吐沙砾。
/// 因此任何"凭空给沙砾"的入口都会破坏 <c>沙砾 + 空隙 = 容量</c> —— 见 <see cref="CheckInvariant" /> 的自检。
/// </para>
/// </remarks>
internal sealed class AeonglassSandSubscription : ISecondaryResourceHookListener
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

        if (context.Definition.Id != AeonglassSecondaryResources.SandId)
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

        var sandBefore = SecondaryResourceCmd.Get(context.Player, AeonglassSecondaryResources.SandId);
        var voidBefore = SecondaryResourceCmd.Get(context.Player, AeonglassSecondaryResources.VoidId);

        _dispatching = true;
        try
        {
            await SecondaryResourceCmd.Gain(
                context.Player,
                AeonglassSecondaryResources.VoidId,
                amount,
                source: null);
        }
        finally
        {
            _dispatching = false;
        }

        var sandAfter = SecondaryResourceCmd.Get(context.Player, AeonglassSecondaryResources.SandId);
        var voidAfter = SecondaryResourceCmd.Get(context.Player, AeonglassSecondaryResources.VoidId);

        Const.Logger.Info(
            $"[{Const.ModId}] [M1] 支付 {amount} 沙砾 → 生成空隙"
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
        if (context.Definition.Id != AeonglassSecondaryResources.SandId
            && context.Definition.Id != AeonglassSecondaryResources.VoidId)
        {
            return;
        }

        if (!IsOurs(context.Player))
        {
            return;
        }

        CheckInvariant(
            context.Player,
            SecondaryResourceCmd.Get(context.Player, AeonglassSecondaryResources.SandId),
            SecondaryResourceCmd.Get(context.Player, AeonglassSecondaryResources.VoidId));
    }

    private void CheckInvariant(Player player, int sand, int gap)
    {
        var state = (sand, gap);
        if (_lastChecked == state)
        {
            return;
        }

        _lastChecked = state;

        if (sand + gap == AeonglassSecondaryResources.Capacity)
        {
            return;
        }

        Const.Logger.Warn(
            $"[{Const.ModId}] [M1] 不变式被破坏：沙砾 {sand} + 空隙 {gap} = {sand + gap}"
            + $"，应为 {AeonglassSecondaryResources.Capacity}。"
            + "（检查是不是有入口在凭空增减其中一个池子）");
    }

    /// <summary>是不是我们角色的玩家（次级资源是模组级资源，谁都能持有，所以必须按角色过滤）。</summary>
    private static bool IsOurs(Player? player) => player?.Character is Aeonglass;
}
