using MegaCrit.Sts2.Core.Rooms;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Stasis;
namespace STS2_Aeonglass.Core.Systems.Bootstrap;

/// <summary>
/// 战斗结束的**显式顺序链**（接缝 2）：<b>双卡组 轮换 → 搁浅 递减 → 搁浅 归队</b>。
/// </summary>
/// <remarks>
/// <para>
/// 为什么要有这么一个"只负责排序"的模型：双卡组 与 搁浅 各有一个 <c>HookedSingletonModel</c>，
/// 而 RitsuLib 的 run 钩子是**按注册/发现顺序**派发的 —— 那是不确定的。
/// 规则层 v1 要求"归队严格发生在卡组切换之后"，所以顺序**不能靠碰运气**：把两件事收进同一条链，
/// 由本类按固定次序调用（这也是方案里"接缝 2"的落点）。
/// </para>
/// <para>
/// 三个段各自打日志（含顺序证据），交接文档里贴的就是这些行。
/// 每一步都自己判断自己的开关 —— 某个机制关掉时只跳过那一段，不影响另一段。
/// </para>
/// </remarks>
[RegisterSingleton]
public sealed class EndOfCombatSequencer : HookedSingletonModel
{
    public EndOfCombatSequencer()
        : base(HookType.Run)
    {
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (CurrentRunState is not { } runState)
        {
            return;
        }

        var m6 = await RotateDecksAsync(runState);
        var m7 = await TickStasisAsync(runState);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 结束序列（显式链）：双卡组 轮换={m6} → 搁浅 递减+归队={m7}");
    }

    /// <summary>第 1 段：双卡组 自动轮换（关掉 双卡组 或关掉自动轮换时跳过）。</summary>
    private static async Task<string> RotateDecksAsync(MegaCrit.Sts2.Core.Runs.IRunState runState)
    {
        // 角色门：别的角色的局里绝不轮换卡组。
        if (!CharacterGate.IsActive)
        {
            return "不是永世沙漏";
        }

        if (!DeckModule.Enabled)
        {
            return "双卡组 未启用";
        }

        if (!AeonglassSettingStore.Current.DecksAutoSwitch)
        {
            return "自动轮换已关";
        }

        var rotated = 0;
        foreach (var player in runState.Players)
        {
            try
            {
                var outcome = await DeckSwitchService.AutoRotateAsync(player);
                if (outcome.Ok)
                {
                    rotated++;
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Error(
                    $"[{Const.ModId}] {Const.Prefix.Decks} 自动轮换抛异常（已跳过）：{ex.GetType().Name}: {ex.Message}");
            }
        }

        return $"已轮换 {rotated} 人";
    }

    /// <summary>第 2 段：搁浅 递减 + 归队（旧口径"轮换前归队"由 <c>StasisReturnBeforeSwitch</c> 控制）。</summary>
    private static async Task<string> TickStasisAsync(MegaCrit.Sts2.Core.Runs.IRunState runState)
    {
        if (!ExtraPileModules.StasisEnabled)
        {
            return "搁浅 未启用";
        }

        if (AeonglassSettingStore.Current.StasisReturnBeforeSwitch)
        {
            // 回退口径：归队在 双卡组 轮换之前发生（由 StasisRunHook 的 AfterCombatEnd 负责），这里不重复。
            return "旧口径（轮换前归队，见 StasisRunHook）";
        }

        await StasisZone.TickAfterCombatEnd(runState);
        return "已递减+归队（在轮换之后）";
    }
}
