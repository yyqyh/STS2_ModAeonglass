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
/// 乱流的<b>日志侧</b>：状态切换与"哪张牌从哪张堆离开"的取证日志（含每场上限）。
/// </summary>
/// <remarks>
/// ① 职责：<c>NoteStateChange</c>（切换才打一行）+ <c>LogAndReturn</c>（对调命中的节流日志）。
/// ② 调用时机：<c>RiotSwap</c> 核心判定命中 / 状态切换时；事件侧在 <c>RiotPileWatch</c>。
/// ③ 依赖：<c>RiotSwap</c>（状态真身）。
/// ④ ⚠️ 日志只做取证，别在这里改状态。
/// </remarks>
internal static class RiotSwapLog
{

    internal static CardPile LogAndReturn(PlayerCombatState state, CardPile redirected, CardPile original)
    {
        RiotSwap.RedirectHits++;
        NoteStateChange(state, active: true);

        var now = Godot.Time.GetTicksMsec();
        if (now - RiotSwap.LastRedirectLogMs >= 3000)
        {
            RiotSwap.LastRedirectLogMs = now;
            Const.Logger.Info(
                $"[{Const.ModId}] [乱流] getter 替换生效：{original.Type} → {redirected.Type}（累计 {RiotSwap.RedirectHits} 次）");
        }

        return redirected;
    }

    /// <summary>状态切换时打一行（点亮 / 失效），只在真的变了的时候打。</summary>
    internal static void NoteStateChange(PlayerCombatState state, bool active)
    {
        if (RiotSwap.LastActive.TryGetValue(state, out var last) && last == active)
        {
            return;
        }

        var firstTime = !RiotSwap.LastActive.ContainsKey(state);
        RiotSwap.LastActive[state] = active;

        // 第一次见到这个 state 且"未生效"只是默认状态，不值得打一行。
        if (firstTime && !active)
        {
            return;
        }

        Const.Logger.Info(
            active
                ? $"[{Const.ModId}] [乱流] 生效（乱流在弃牌堆里）：抽/弃/洗牌等全部对调"
                : $"[{Const.ModId}] [乱流] 失效（乱流不在弃牌堆里）：恢复原版");
    }
}
