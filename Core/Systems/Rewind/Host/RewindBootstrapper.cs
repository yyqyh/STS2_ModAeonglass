using STS2RitsuLib;

using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems;

using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Rewind.Host;

/// <summary>
/// 回溯的<b>装配入口</b>：把门禁/快照环/扩展接缝按顺序装好。
/// </summary>
/// <remarks>
/// ① 职责：构建 <c>RewindFacade</c> 并注册三条接缝。
/// ② 调用时机：<c>RewindModule.Register</c>。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

public static class RewindBootstrapper
{
    private static bool _started;

    public static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        var facade = RewindFacade.Instance;

        // 角色门（2026-10-15）：别人的局里连"战斗开始/结束"的抓拍与收尾都不做
        //（实测漏过：静默猎手局里引擎照样每回合拍快照、日志里留了 combat started / ended）。
        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(_ => Guarded("combat starting", () =>
        {
            if (CharacterGate.IsActive)
            {
                facade.OnCombatStarted();
            }
        }));

        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(_ => Guarded("combat ended", () =>
        {
            if (CharacterGate.IsActive)
            {
                facade.OnCombatEnded();
            }
        }));

        // 回合打点：玩家侧回合开始（此时开局管线已完成、相位为 Play）。
        CombatManager.Instance.TurnStarted += state =>
        {
            // ⚠️ 这是**本体回合循环里的同步回调**（`CombatManager.StartTurn` 直接调它）：
            //    这里漏一个异常出去，本体就 "turn loop died … the combat is stuck" —— 战斗卡死、
            //    回合结束按键不出现、只能重启房间（2026-10-16 被克隆器的 HashSet 缺陷实测炸到）。
            //    ⇒ 整段包兜底，任何异常只记日志，快照跳过、本体照常走。
            Guarded("turn started capture", () =>
            {
                // 角色门：不是永世沙漏的局里不拍快照（否则别的角色也会背上这份开销与 VFX 屏障）。
                if (state.CurrentSide == CombatSide.Player && CharacterGate.IsActive)
                {
                    facade.CaptureTurnStart();
                }
            });
        };

        facade.Log.Info($"rewind lab ready v{Const.Version}（回档粒度：上一回合）");
    }

    /// <summary>
    /// 把一段"跑在本体流程里"的回调包成<b>永不外泄</b>：异常只降级（记日志 + 跳过本次），
    /// 绝不让它顺着本体的事件/回合循环往外冒（纪律见 <c>Core/Patches/README.md</c>）。
    /// </summary>
    private static void Guarded(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Rewind} 回调异常（{what}，已降级为跳过本次）："
                + $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
