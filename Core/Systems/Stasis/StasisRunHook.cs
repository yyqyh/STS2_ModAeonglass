using STS2_Aeonglass.Core.Config;

using MegaCrit.Sts2.Core.Rooms;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>搁浅的 run 级驱动者：战斗结束时推进滞留倒计时。</summary>
/// <remarks>
/// ① 职责：订阅 <c>AfterCombatEnd</c>，调 <c>StasisReturn.TickAfterCombatEnd</c> 递减搁浅倒计时。
/// ② 挂点与时机：<c>[RegisterSingleton]</c> + <c>HookType.Run</c>，每场战斗结束触发一次。
/// ③ 依赖：<c>StasisStore</c> / <c>StasisReturn</c>；归队顺序由 <c>EndOfCombatSequencer</c> 的显式链统一驱动。
/// ④ ⚠️ 搁浅里的牌收不到任何 hook（<c>RunState.IterateHookListeners</c> 只遍历 <c>player.Deck.Cards</c>，Lab A 已证伪
/// "卡自身的 <c>AfterCombatEnd</c>"）⇒ 倒计时只能由这个 run 级订阅者驱动；默认不在这里归队，
/// 只有回退开关 <c>mechanism_m7_return_before_switch</c> 打开时才走旧口径（= 归队发生在卡组轮换之前）。
/// </remarks>
[RegisterSingleton]

public sealed class StasisRunHook : HookedSingletonModel
{
    public StasisRunHook()
        : base(HookType.Run)
    {
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        // 记录"刚打完的是不是 Boss 房"（「远行」在 Boss 层回归要随机升级卡组）。
        // 放在角色门 / 口径分支之前：无论走新口径还是回退口径，这个事实都要记。
        StasisReturn.LastCombatWasBoss = room.RoomType == RoomType.Boss;

        // 角色门（2026-10-15）：别人的局里不递减、不归队（回退口径那条日志也不会出现）。
        if (!ExtraPileModules.StasisEnabled || !CharacterGate.IsActive)
        {
            return;
        }

        if (!AeonglassSettingStore.Current.StasisReturnBeforeSwitch)
        {
            // 新口径：归队交给 EndOfCombatSequencer（在 双卡组 轮换之后）。
            return;
        }

        if (CurrentRunState is not { } runState)
        {
            return;
        }

        await StasisReturn.TickAfterCombatEnd(runState);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 归队走的是**旧口径**（轮换之前，回退开关已开）");
    }
}
