using Godot;

using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;

using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Relics;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;
using STS2_Aeonglass.Core.Systems.Rewind.Bridges;
using STS2_Aeonglass.Core.Systems.Rewind.Host;
using STS2_Aeonglass.Core.Systems.Rewind.Host.Patches;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;
using STS2_Aeonglass.Core.Systems.Sand;

using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Rewind;

/// <summary>回溯模块入口：装配引擎 + 接三条接缝 + 对外唯一触发入口。</summary>
/// <remarks>
/// ① 职责：<c>Register</c>、<c>CanRequest</c> / <c>TryRequest</c>（含代价支付 <c>RewindCost</c>）、<c>Describe</c>。
/// ② 挂点与时机：由 <c>MechanismBootstrap</c> 按开关调用；UI / 热键都走这里。
/// ③ 依赖：<c>Rewind/{Kernel,StateCapture,Graph,Policy,Host}</c> + <c>Bridges/</c> 的接缝。
/// ④ ⚠️ 引擎代码是从 <c>STS2_AeonglassLabB</c> 原样搬来的（只改命名空间）—— <b>不要在这里改引擎</b>，宿主侧（接缝 / 开关 / 代价 / UI）收在本文件与 <c>Bridges/</c>；
/// 定档参数：每场战斗 1 次 + 成功后消耗全部沙砾、只回上一回合、单人限定（多人单端改状态会让校验和分化）、快照不跨会话。
/// </remarks>
internal static class RewindModule
{
    /// <summary>日志 / 设置页里显示的机制名。</summary>
    public const string DisplayName = "回溯";

    private static bool _registered;

    /// <summary>回溯 是否启用。</summary>
    public static bool Enabled => AeonglassSettingStore.Current.RewindEnabled;

    /// <summary>由 <c>MechanismBootstrap</c> 逐条注册调用。</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        var facade = RewindFacade.Instance;

        // ① 接缝 回溯 ↔ 沙砾：**Step 2 起资源桥退场**。
        //    口径（规则层 v1 · P2）：沙砾 / 空隙已是 **run 级跨战斗资源**，回溯**不还原**它们 ——
        //    否则"回档返还资源"会让 回溯的代价（消耗全部沙砾）白付。
        //    实现 `AeonglassSecondaryResourceBridge` 已随整理删除（git 历史里可取回）：留着它只会让"这条接缝还在"产生误解。
        //    真要恢复"回档还原资源"，照它当时的写法重接 ISecondaryResourceBridge 即可。
        //    **不再注册** ⇒ 引擎走 NullSecondaryResourceBridge：快照不抓资源、恢复也不动资源。
        SecondaryResourceRuntime.Bridge = NullSecondaryResourceBridge.Instance;
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Rewind} 沙砾 / 空隙**不参与回溯**（Step 2：资源桥退场，回档后保持当前值）"
            + $"｜旧的 mechanism_m1_in_rewind 开关已失效（保留字段只为老配置兼容）");

        // ② 接缝 回溯 ↔ 搁浅 / 间隙 / 敌人时间（order 100 / 200 / 300）。
        facade.Extensions.Add(new StasisRewindExtension());
        facade.Extensions.Add(new GapRewindExtension());
        facade.Extensions.Add(new DeferralRewindExtension());
        facade.Extensions.Add(new AlphaBetaRewindExtension());

        // ③ VFX 屏障补丁：12 个异步卡 VFX 目标，逐个手动装（一个改名不会带崩整组）。
        var harmony = new Harmony(Const.ModId + ".rewind");
        var (vfxOk, vfxFailed) = VfxTrackingPatches.Install(
            harmony,
            warn => Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Rewind} {warn}"));

        // ④ 宿主装配：生命周期订阅 + 回合开始打点。
        RewindBootstrapper.Start();

        // ⑤ 事件遗物「破碎的怀表」的获取入口（用户 2026-10-14：**改事件获得、另开、紧随捏奥之后**）：
        //    事件本体在 Content/Events/AeonglassBrokenWatchEvent.cs，
        //    现场那条钩子在 Core/Patches/BrokenWatchEventChainPatch.cs（随 Main 的补丁扫描一起装），
        //    这里补的是"房间进入"那条（读档恢复时本体不会再触发那个钩子，实测漏过一次）。
        RitsuLibFramework.SubscribeLifecycle<RoomEnteredEvent>(evt =>
        {
            try
            {
                switch (evt.Room)
                {
                    case MegaCrit.Sts2.Core.Rooms.EventRoom { CanonicalEvent: MegaCrit.Sts2.Core.Models.Events.Neow } ancient:
                        Systems.Rewind.BrokenWatchChain.HandleAncientRoomEntered(
                            ancient, evt.RunState.Players, "先古房间进入（含读档恢复）");
                        break;
                    default:
                        foreach (var player in evt.RunState.Players)
                        {
                            Systems.Rewind.BrokenWatchChain.TryChainAtRest(player, evt.Room);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Rewind} 事件链（房间进入）失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
            }
        });

        // ⚠️ 先前"跑局开始直接发放"与"捏奥界面加一个选项"两条临时做法都已按用户口径删除。

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Rewind} 回溯引擎就绪："
            + $"快照环容量 {facade.Ring.Capacity}，接缝 {facade.Extensions.Items.Count} 条"
            + $"（{string.Join(",", facade.Extensions.Items.Select(e => $"{e.Name}@{e.Order}"))}），"
            + $"VFX 屏障补丁 {vfxOk} 成功 / {vfxFailed} 失败");
    }

    /// <summary>一行状态（调试面板用）。</summary>
    public static string Describe(Player? player)
    {
        var facade = RewindFacade.Instance;
        var used = player is not null && RewindCombatLimit.Used(player) ? 1 : 0;
        var sand = player is null || SandResourceRegistry.Sand is null
            ? "-"
            : SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId).ToString();

        return $"快照 {facade.Ring.Items.Count}/{facade.Ring.Capacity}"
               + $" ｜ 本场已用 {used}/1 ｜ 沙砾 {sand} ｜ {facade.State.Phase}";
    }

    /// <summary>
    /// <b>从"排队的右键动作"里发起回溯</b>（遗物右键入口专用）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 为什么不能直接调 <see cref="TryRequest" />：右键走 RitsuLib 的**同步动作**，我们的回调是在
    /// **动作队列里**执行的；而回溯的闸门里有一条"队列必须为空（<c>IsRuntimeSettled</c>）"，
    /// 于是同一个动作里发起的回溯**必然**被拦（实测日志：`回档被拦截：无挂起工作（action queue is not empty）`）。
    /// </para>
    /// <para>
    /// 做法：立刻返回（让这个动作先出队），在后台等运行期稳定下来（队列/执行器/效果深度都闲），
    /// 再走正常入口。最多等 60 帧（≈1s），等不到就让闸门去拒绝并给出原因。
    /// </para>
    /// </remarks>
    public static void RequestFromQueuedAction(Player player) =>
        TaskHelper.RunSafely(RequestFromQueuedActionAsync(player));

    private static async Task RequestFromQueuedActionAsync(Player player)
    {
        for (var frame = 0; frame < 60; frame++)
        {
            if (CombatAccess.IsRuntimeSettled(out _))
            {
                break;
            }

            await WaitFrameAsync();
        }

        if (TryRequest(player, out var message))
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 右键回溯已发起（等队列排空后）：{message}");
        }
        else
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Rewind} 右键回溯被拒：{message}");
        }
    }

    private static async Task WaitFrameAsync()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            await tree.ToSignal(tree, "process_frame");
            return;
        }

        await Task.Delay(16);
    }

    /// <summary>
    /// 现在能不能请求回溯（给"右键遗物"的可用性判定用）。
    /// </summary>
    /// <remarks>
    /// 只做**廉价且稳定**的判断（角色门 + 本场是否已用过）；闸门细节与"有没有上一回合快照"
    /// 交给 <see cref="TryRequest" /> 在真正按下时判定并回话，避免按钮状态与真实可用性打架。
    /// </remarks>
    public static bool CanRequest(Player player) =>
        CharacterGate.IsActive && !RewindCombatLimit.Used(player);

    /// <summary>
    /// 唯一触发入口（现在由调试按键调用，将来由卡牌 / 遗物调用）。
    /// </summary>
    /// <remarks>
    /// <b>代价为什么在"回档成功之后"才收</b>：时之沙的沙砾属于回档范围（<c>ISecondaryResourceBridge</c>），
    /// 回档会把沙砾恢复成快照值 —— 如果先收，收掉的沙砾会被恢复动作**还回来**，代价等于不存在。
    /// 所以：先占位 → 发回档 → 成功后**把此刻（= 已恢复）的沙砾全部扣掉**；回档没成立就退回占位。
    /// </remarks>
    public static bool TryRequest(Player player, out string message)
    {
        var facade = RewindFacade.Instance;

        if (!CharacterGate.IsActive)
        {
            message = "回溯只对永世沙漏生效";
            return false;
        }

        if (RewindCombatLimit.Used(player))
        {
            message = "本场战斗已经回溯过（每场 1 次）";
            return false;
        }

        var charged = AeonglassSettingStore.Current.RewindCostAllSand
                      && SandResourceRegistry.Sand is not null;

        RewindCombatLimit.Mark(player);
        AeonglassRelicBadges.Refresh(player);
        message = charged ? "回溯中…（成功后消耗全部沙砾）" : "回溯中…";

        facade.RequestRewindToPreviousTurn(ok =>
        {
            if (!ok)
            {
                RewindCombatLimit.Unmark(player);
                AeonglassRelicBadges.Refresh(player);
                return;
            }

            if (charged)
            {
                TaskHelper.RunSafely(RewindCost.SpendAllSandAsync(player));
            }

            // 战斗历史：本场回溯次数 +1（RW 通道的卡读它）。
            Systems.History.CombatHistory.RecordRewind(player);
            // 本局历史：跨战斗累计回溯次数 +1（v2「因果」读它）。
            Systems.History.RunHistory.RecordRewind(player);
        });

        return true;
    }

}
