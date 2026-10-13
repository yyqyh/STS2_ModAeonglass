using MegaCrit.Sts2.Core.Rooms;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// M6 的 run 级驱动：<b>战斗结束</b>记冷却、<b>战斗胜利</b>自动轮换。
/// </summary>
/// <remarks>
/// <para>
/// 为什么用 singleton 而不是挂在遗物上：它不属于任何一件具体遗物，是"这一局"的机制
/// （和 M7 的 <c>StasisRunHook</c> 同一个理由）。
/// </para>
/// <para>
/// <b>为什么轮换放在 <c>AfterCombatVictory</c> 而不是 <c>AfterCombatEnd</c></b>：
/// 本体的战斗收尾顺序是 <c>Hook.AfterCombatEnd</c> → 各玩家 <c>AfterCombatEnd</c> →
/// <c>Hook.AfterCombatVictory</c>（<c>CombatManager</c> 里就是这么排的）。
/// M7 的滞留倒计时在 <c>AfterCombatEnd</c> 归队，归队目标是"当时生效的那副" ——
/// 把轮换放在后面的那个钩子里，**顺序由本体保证**，不需要赌两个 singleton 的注册顺序，
/// 于是"滞留卡回到它待过的那副"是稳定行为。（堕化拉撒路也是在 <c>AfterCombatVictory</c> 切卡组。）
/// </para>
/// </remarks>
[RegisterSingleton]
public sealed class DeckRunHook : HookedSingletonModel
{
    public DeckRunHook()
        : base(HookType.Run)
    {
    }

    /// <summary>每场战斗结束：推进"主动切换冷却"的计时（不管自动轮换开没开）。</summary>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (!DecksModule.Enabled || CurrentRunState is not { } runState)
        {
            return Task.CompletedTask;
        }

        foreach (var player in runState.Players)
        {
            DeckState.NoteCombatEnded(player);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 战斗胜利：<b>自动轮换已移到 <c>EndOfCombatSequencer</c> 的显式链里</b>（Step 3 · 接缝 2）。
    /// </summary>
    /// <remarks>
    /// 移出去的原因：M7 的归队必须严格发生在本轮换**之后**，而两个 singleton 的钩子派发顺序不确定
    /// ⇒ 收进一条显式链（M6 轮换 → M7 递减 → M7 归队）。本类只保留"每场结束推进冷却计时"。
    /// </remarks>
    public override Task AfterCombatVictory(CombatRoom room)
    {
        return Task.CompletedTask;
    }
}
