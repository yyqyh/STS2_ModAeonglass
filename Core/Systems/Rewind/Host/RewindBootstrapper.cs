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

        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(_ => facade.OnCombatStarted());
        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(_ => facade.OnCombatEnded());

        // 回合打点：玩家侧回合开始（此时开局管线已完成、相位为 Play）。
        CombatManager.Instance.TurnStarted += state =>
        {
            // 角色门：不是永世沙漏的局里不拍快照（否则别的角色也会背上这份开销与 VFX 屏障）。
            if (state.CurrentSide == CombatSide.Player && CharacterGate.IsActive)
            {
                facade.CaptureTurnStart();
            }
        };

        facade.Log.Info($"rewind lab ready v{Const.Version}（回档粒度：上一回合）");
    }
}
