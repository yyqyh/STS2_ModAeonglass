using STS2RitsuLib;

using MegaCrit.Sts2.Core.Combat;

using STS2_Aeonglass.Core.Systems;

using STS2_Aeonglass.Core.Systems.Bootstrap;
namespace STS2_Aeonglass.Core.Systems.Rewind.Host;

/// <summary>宿主装配：生命周期订阅 + 引擎就绪日志。只在这里用 RitsuLib。</summary>
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
