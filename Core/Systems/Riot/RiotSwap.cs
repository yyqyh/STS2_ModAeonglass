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
/// 乱流的<b>判定与对调核心</b>：乱流在（物理）弃牌堆里 ⇒ 抽/弃/洗全部对调。
/// </summary>
/// <remarks>
/// ① 职责：IsActive / Redirect / 真身缓存（状态与对调落地见 <c>RiotSwap.State</c>/<c>.FieldSwap</c>）。
/// ② 调用时机：属性读取与牌堆事件。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static partial class RiotSwap
{
    /// <summary>一个玩家这一场战斗里的三张堆（记的是<b>物理</b>真身，不随对调变化）。</summary>
    internal sealed class PilePair
    {
        public required CardPile Draw { get; init; }

        public required CardPile Discard { get; init; }

        public required CardPile Hand { get; init; }

        /// <summary>当前是否处于对调状态（字段方案 = 字段已交换；兜底方案 = 判定结果）。</summary>
        public bool Swapped { get; set; }

        /// <summary>字段方案能不能用（首次写失败就永久关掉，改走 getter 兜底）。</summary>
        public bool FieldSwapWorks { get; set; } = true;

        /// <summary>"正在抽取"的嵌套层数（&gt;0 期间冻结状态：这一次抽牌抽到什么就是什么）。</summary>
        public int DrawDepth { get; set; }

        /// <summary>抽取期间被推迟的状态变化（抽完立刻结算）。</summary>
        public bool PendingSync { get; set; }

        /// <summary>本轮抽取有没有报过"冻结"那一行（防刷屏）。</summary>
        public bool DeferredLogged { get; set; }

        public bool CanWriteFields => FieldSwapWorks && DrawField is not null && DiscardField is not null;
    }

    /// <summary>编译器生成的自动属性字段（没有它就没法做"真对调"）。</summary>
    internal static readonly FieldInfo? DrawField = FindPileField("DrawPile");

    internal static readonly FieldInfo? DiscardField = FindPileField("DiscardPile");

    /// <summary><c>PlayerCombatState._piles</c>：<c>AllPiles</c> 的懒建缓存，交换后要置空重建。</summary>
    internal static readonly FieldInfo? AllPilesField = typeof(PlayerCombatState)
        .GetField("_piles", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static readonly ConditionalWeakTable<PlayerCombatState, PilePair> Piles = new();

    /// <summary>已挂过订阅的堆（防重复）。</summary>
    internal static readonly HashSet<CardPile> Watched = [];

    /// <summary>上一次打日志时的激活状态（只在"切换"那一刻打一行）。</summary>
    internal static readonly Dictionary<PlayerCombatState, bool> LastActive = [];

    internal static int RedirectHits;

    internal static ulong LastRedirectLogMs;

    /// <summary>牌堆转出明细日志的行数（每场上限；用来肉眼核对"牌到底动没动"）。</summary>
    internal static int MoveLogCount;

    internal const int MoveLogLimit = 80;

    internal const int DeferLogLimit = 12;

    internal static bool SwapModeLogged;

    internal static int DeferLogCount;

    /// <summary>机制总开关（默认开）。</summary>
    internal static bool Enabled => AeonglassSettingStore.Current.RiotEnabled;

    /// <summary>抓"真身"时临时屏蔽对调（否则自己读到的是被换过的那张）。</summary>
    internal static bool Suppressed { get; set; }

    /// <summary>现在要不要对调（连续判定：乱流在不在物理弃牌堆里）。</summary>
    public static bool IsActive(PlayerCombatState state)
    {
        if (AeonglassSettingStore.Current.RiotForceSwap)
        {
            return true;
        }

        return Enabled && RiotInDiscard(state);
    }

    /// <summary>
    /// 这个战斗态是否已在我们的乱流台账里。
    /// </summary>
    /// <remarks>
    /// 给补丁做**快速短路**用：乱流没在场的局（含别的角色 / 原版）在这里就返回 false，
    /// 后面的反射核对与日志一次都不做 ⇒ 热路径上只剩一次弱表查询。
    /// </remarks>
    internal static bool HasPair(PlayerCombatState? state)
    {
        if (state is null)
        {
            return false;
        }

        return Piles.TryGetValue(state, out _);
    }

    // ============================ 收口 ============================

    /// <summary>
    /// 补丁的落点（<c>PlayerCombatState.DrawPile / DiscardPile</c> 两个 getter 的 postfix）。
    /// 正常情况下这里只是"顺带核对一次状态"：真正负责对调的是属性字段本身。
    /// 只有在字段方案不可用时，才在这里把按类型取到的堆换成对面那张。
    /// </summary>
    public static CardPile Redirect(PlayerCombatState state, CardPile pile)
    {
        if (Suppressed || !Piles.TryGetValue(state, out var pair))
        {
            return pile;
        }

        // 读属性时顺带核对状态（字段方案的保险丝：万一某个切换点没挂到事件，这里也能补上）。
        SyncSwap(state, pair);

        if (pair.FieldSwapWorks)
        {
            // 字段已经兜住：属性读出来就是对面那张，这里再换一次就等于换回去。
            return pile;
        }

        if (!IsActive(state))
        {
            RiotSwapLog.NoteStateChange(state, active: false);
            return pile;
        }

        return pile.Type switch
        {
            PileType.Draw => ReferenceEquals(pile, pair.Draw) ? RiotSwapLog.LogAndReturn(state, pair.Discard, pile) : pile,
            PileType.Discard => ReferenceEquals(pile, pair.Discard) ? RiotSwapLog.LogAndReturn(state, pair.Draw, pile) : pile,
            _ => pile,
        };
    }

    // ============================ 接线 ============================

    // ============================ 内部 ============================

    /// <summary>乱流现在在不在（物理）弃牌堆里。</summary>
    private static bool RiotInDiscard(PlayerCombatState state)
    {
        if (!Piles.TryGetValue(state, out var pair))
        {
            CachePiles(state);
            if (!Piles.TryGetValue(state, out pair))
            {
                return false;
            }
        }

        return pair.Discard.Cards.Any(card => card is AeonglassRiot);
    }

}
