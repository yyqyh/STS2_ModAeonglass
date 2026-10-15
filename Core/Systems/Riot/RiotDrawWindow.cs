using System.Reflection;
using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Curse;

namespace STS2_Aeonglass.Core.Systems.Riot;

/// <summary>
/// 乱流的<b>抽取窗口</b>：一次抽牌的整个 Task 期间冻结对调状态（抽完再结算）。
/// </summary>
/// <remarks>
/// ① 职责：开窗/关窗与"抽取期间攒下的状态变化"的结算；嵌套抽牌用层数计数。
/// ② 调用时机：<c>RiotDrawWindowPatch</c> 包住 <c>CardPileCmd.Draw</c> 的 Task。
/// ③ 依赖：<c>RiotSwap</c>（真身与 RiotSwap.SyncSwap）。
/// ④ ⚠️ 关窗在 <c>finally</c> 里做（抽牌异常也要解锁）；窗口内所有状态切换都被推迟。
/// </remarks>
internal static class RiotDrawWindow
{

    /// <summary>
    /// <b>抽取窗口</b>：本体每次抽牌（<c>CardPileCmd.Draw</c>）进入时开窗。
    /// 这段时间里所有状态切换都被冻结（见 <see cref="RiotSwap.SyncSwap" />），因为
    /// <c>DrawInternal</c> 是"局部变量抱住一张堆 + 每张牌重新读属性"的写法，中途换堆会错位。
    /// </summary>
    public static void EnterDraw(Player player)
    {
        if (player.PlayerCombatState is not { } state)
        {
            return;
        }

        RiotSwap.CachePiles(state);
        if (RiotSwap.Piles.TryGetValue(state, out var pair))
        {
            pair.DrawDepth++;
            pair.DeferredLogged = false;
        }
    }

    /// <summary>
    /// 包住本体抽牌的 <c>Task</c>：抽完（含异常）退出窗口，并把抽取期间攒下的状态变化结算掉。
    /// </summary>
    public static async Task<IEnumerable<CardModel>> TrackDraw(
        Player player,
        Task<IEnumerable<CardModel>> inner)
    {
        try
        {
            return await inner;
        }
        finally
        {
            ExitDraw(player);
        }
    }

    private static void ExitDraw(Player player)
    {
        if (player.PlayerCombatState is not { } state || !RiotSwap.Piles.TryGetValue(state, out var pair))
        {
            return;
        }

        if (pair.DrawDepth > 0)
        {
            pair.DrawDepth--;
        }

        if (pair.DrawDepth != 0 || !pair.PendingSync)
        {
            return;
        }

        pair.PendingSync = false;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Riot} 抽取结束 ⇒ 结算冻结期间的状态");
        RiotSwap.SyncSwap(state, pair);
    }

    /// <summary>同上，给返回 <c>Task</c>（无返回值）的那类"D 堆循环"（如 <c>AutoPlayFromDrawPile</c>）。</summary>
    public static async Task TrackDraw(Player player, Task inner)
    {
        try
        {
            await inner;
        }
        finally
        {
            ExitDraw(player);
        }
    }
}
