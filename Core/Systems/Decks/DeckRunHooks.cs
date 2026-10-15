using MegaCrit.Sts2.Core.Rooms;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>双卡组的 run 级驱动：<b>战斗结束</b>记冷却、<b>战斗胜利</b>自动轮换。</summary>
/// <remarks>
/// ① 职责：把 run 事件接到卡组切换与"主动切换冷却"的计时上。
/// ② 挂点与时机：<c>[RegisterSingleton]</c> + <c>HookType.Run</c> 的 <c>AfterCombatEnd</c> / <c>AfterCombatVictory</c>。
/// ③ 依赖：<c>DeckSwitchService</c>、<c>DeckSwitchLimit</c>、设置项。
/// ④ ⚠️ 轮换放在 <c>AfterCombatVictory</c> 而不是 <c>AfterCombatEnd</c>：本体收尾顺序是 <c>Hook.AfterCombatEnd</c> →
/// 各玩家 <c>AfterCombatEnd</c> → <c>Hook.AfterCombatVictory</c>，搁浅递减在前、轮换在后 ⇒ "滞留卡回到它待过的那副"由本体顺序保证，
/// 不赌两个 singleton 的注册顺序（堕化拉撒路也在 <c>AfterCombatVictory</c> 切卡组）。用 singleton 是因为它属于"这一局"而非某件遗物。
/// </remarks>
[RegisterSingleton]

public sealed class DeckRunHooks : HookedSingletonModel
{
    public DeckRunHooks()
        : base(HookType.Run)
    {
    }

    /// <summary>每场战斗结束：推进"主动切换冷却"的计时（不管自动轮换开没开）。</summary>
    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (!DeckModule.Enabled || CurrentRunState is not { } runState)
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
    /// 移出去的原因：搁浅的归队必须严格发生在本轮换**之后**，而两个 singleton 的钩子派发顺序不确定
    /// ⇒ 收进一条显式链（双卡组 轮换 → 搁浅 递减 → 搁浅 归队）。本类只保留"每场结束推进冷却计时"。
    /// </remarks>
    public override Task AfterCombatVictory(CombatRoom room)
    {
        return Task.CompletedTask;
    }
}
