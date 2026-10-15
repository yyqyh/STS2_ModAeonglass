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
/// 乱流的<b>接线与取证</b>：订阅牌堆事件、按状态切换对调、并把"哪张牌从哪张堆离开"写进日志。
/// </summary>
/// <remarks>
/// ① 职责：<c>Register</c> 建订阅；乱流进/出弃牌堆、抽/弃牌堆被动过时重新核对状态；每回合报一次快照。
/// ② 调用时机：机制总线注册时；之后由牌堆事件与回合开始事件驱动。
/// ③ 依赖：<c>RiotSwap</c>（判定与对调核心、弱表里的两张真身）+ 意图/迟滞等无关（这里只认"乱流在不在弃牌堆"）。
/// ④ ⚠️ 状态切换一律走 <c>RiotSwap.SyncSwap</c>，本类不自己写字段。
/// </remarks>
internal static class RiotPileWatch
{

    /// <summary>由机制总线注册（接线永远装，开关只 gate 行为）。</summary>
    public static void Register()
    {
        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(evt =>
        {
            foreach (var player in evt.CombatState?.Players ?? [])
            {
                if (player.PlayerCombatState is not { } state)
                {
                    continue;
                }

                RiotSwap.CachePiles(state);
                WatchPiles(player, state);
                ReportState(state, "战斗开始");
            }
        });

        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(_ =>
        {
            RiotSwap.Piles.Clear();
            RiotSwap.Watched.Clear();
            RiotSwap.LastActive.Clear();
            RiotSwap.RedirectHits = 0;
            RiotSwap.MoveLogCount = 0;
            RiotSwap.DeferLogCount = 0;
        });

        // 每回合核对一次（也兜住"乱流是中途用控制台塞进去的"这种没经过 CardAdded 的情况）。
        RitsuLibFramework.SubscribeLifecycle<SideTurnStartedEvent>(evt =>
        {
            if (evt.Side != CombatSide.Player)
            {
                return;
            }

            foreach (var player in evt.CombatState?.Players ?? [])
            {
                if (player.PlayerCombatState is { } state)
                {
                    ReportState(state, "回合开始");
                }
            }
        });

        Const.Logger.Info(
            $"[{Const.ModId}] [乱流] 已就绪（总开关={RiotSwap.Enabled}，调试强制互换="
            + $"{AeonglassSettingStore.Current.RiotForceSwap}）"
            + $"｜收口=属性字段对调（{RiotSwap.DrawField?.Name ?? "未找到"} / {RiotSwap.DiscardField?.Name ?? "未找到"}）"
            + "｜口径：乱流在弃牌堆里 ⇒ 抽/弃/洗牌等全部对调；离开即失效");
    }

    private static void WatchPiles(Player player, PlayerCombatState state)
    {
        if (!RiotSwap.Piles.TryGetValue(state, out var pair) || !RiotSwap.Watched.Add(pair.Discard))
        {
            return;
        }

        // 乱流进弃牌堆 ⇒ 立刻对调；出去 ⇒ 立刻还原。这两条是状态切换的主入口。
        pair.Discard.CardAdded += card =>
        {
            if (card is AeonglassRiot)
            {
                Const.Logger.Info($"[{Const.ModId}] [乱流] 乱流进入弃牌堆 ⇒ 立即生效");
            }

            RiotSwap.SyncSwap(state, pair);
        };

        pair.Discard.CardRemoveFinished += () => RiotSwap.SyncSwap(state, pair);

        // 它也可能被挪进"物理抽牌堆"（对调生效时被打出 / 弃掉）⇒ 同样重新判定。
        pair.Draw.CardAdded += _ => RiotSwap.SyncSwap(state, pair);
        pair.Draw.CardRemoveFinished += () => RiotSwap.SyncSwap(state, pair);

        // 明细取证：牌从哪张（物理）堆离开 —— 这是判断"对调到底有没有作用在牌上"的硬证据。
        // 对调生效时：开局抽 5 张 ⇒ 连出 5 行「从 弃牌堆(物理) 取走 …」（画面上就是"弃牌堆 → 手牌"那条动画）；
        //              弃牌 ⇒ 一行「从 抽牌堆(物理) 取走 …」（打出的牌落进物理抽牌堆）。
        pair.Draw.CardRemoved += card => NoteMove(state, "抽牌堆(物理)", card);
        pair.Discard.CardRemoved += card => NoteMove(state, "弃牌堆(物理)", card);

        Const.Logger.Info($"[{Const.ModId}] [乱流] 已盯住 {player.NetId} 的抽/弃牌堆（进/出都记一行）");
    }

    /// <summary>战斗开始 / 回合开始：核对状态并报一次物理堆快照。</summary>
    private static void ReportState(PlayerCombatState state, string why)
    {
        RiotSwap.CachePiles(state);
        if (!RiotSwap.Piles.TryGetValue(state, out var pair))
        {
            return;
        }

        RiotSwap.SyncSwap(state, pair);

        Const.Logger.Info(
            $"[{Const.ModId}] [乱流] {why}：状态={(pair.Swapped ? "对调中" : "原版")}"
            + $"｜抽牌堆(物理) {pair.Draw.Cards.Count} / 弃牌堆(物理) {pair.Discard.Cards.Count}"
            + $"（游戏看到的：抽 {state.DrawPile.Cards.Count} / 弃 {state.DiscardPile.Cards.Count}）");
    }

    private static void NoteMove(PlayerCombatState state, string pileName, CardModel card)
    {
        if (RiotSwap.MoveLogCount >= RiotSwap.MoveLogLimit || !RiotSwap.IsActive(state) || !RiotSwap.Piles.TryGetValue(state, out var pair))
        {
            return;
        }

        RiotSwap.MoveLogCount++;
        Const.Logger.Info(
            $"[{Const.ModId}] [乱流] 从 {pileName} 取走 {card.Id.Entry}"
            + $"（现 抽牌堆(物理) {pair.Draw.Cards.Count} / 弃牌堆(物理) {pair.Discard.Cards.Count}）");
    }
}
