using STS2_Aeonglass.Core.Config;

using MegaCrit.Sts2.Core.Rooms;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

using STS2_Aeonglass.Core.Systems.Bootstrap;
namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>搁浅的倒计时驱动者：<b>mod 的 run 级订阅者</b>。</summary>
/// <remarks>
/// <para>
/// 为什么必须有它：<b>搁浅里的牌收不到任何 hook</b> —— <c>RunState.IterateHookListeners</c> 只遍历
/// <c>player.Deck.Cards</c>，场外牌堆里的牌不在其中（Lab A 用"卡自身的 <c>AfterCombatEnd</c>"做过对照，证伪）。
/// 所以倒计时与归队只能由这个订阅者在 <c>AfterCombatEnd</c> 里驱动。
/// </para>
/// <para>为什么用 singleton 而不是挂在遗物/角色上：它不属于任何一件具体遗物，是"这一局"的机制。</para>
/// <para>
/// <b>Step 3 起默认不再在这里归队</b>：规则层 v1 要求"归队严格发生在 双卡组 卡组轮换之后"，
/// 两者现在由 <c>EndOfCombatSequencer</c> 的显式链驱动（<c>AfterCombatVictory</c>）。
/// 本方法只在**回退开关** <c>mechanism_m7_return_before_switch</c> 打开时生效（= 旧口径）。
/// </para>
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
        if (!ExtraPileModules.StasisEnabled)
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

        await StasisZone.TickAfterCombatEnd(runState);
        Const.Logger.Info($"[{Const.ModId}] [搁浅] 归队走的是**旧口径**（轮换之前，回退开关已开）");
    }
}
