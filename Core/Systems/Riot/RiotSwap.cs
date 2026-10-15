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
/// <b>乱流</b>：只要<b>它在弃牌堆里</b>，本体对"抽牌堆 / 弃牌堆"的<b>所有操作全部对调</b>
/// （抽 / 弃 / 洗牌 / 洗回 / 检索 / 放回堆顶 / 计数，一个不落）；<b>它一离开弃牌堆就立刻失效</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>收口 = 把两张堆在 <see cref="PlayerCombatState"/> 上真的换过来</b>。
/// 本体的取堆最终都落在 <c>DrawPile</c> / <c>DiscardPile</c> 这两个自动属性上，
/// 而自动属性背后就是编译器生成的字段 <c>&lt;DrawPile&gt;k__BackingField</c> /
/// <c>&lt;DiscardPile&gt;k__BackingField</c> ⇒ 交换这两个字段 = 抽 / 弃 / 洗牌 / 洗回 / 检索全部对调。
/// </para>
/// <para>
/// <b>为什么不能只挂属性 getter</b>（2026-10-14 实测踩过）：<c>DrawPile</c> / <c>DiscardPile</c>
/// 是 3 条 IL 的自动属性 getter，JIT 会把它们<b>内联</b>进 <c>CardPile.Get</c> /
/// <c>PileTypeExtensions.GetPile</c> / <c>CardPileCmd.DrawInternal</c> 这些调用点。Harmony 只能改
/// 方法本体，改不了已经内联展开的副本 ⇒ 只挂 getter 时本体的抽牌、弃牌、洗牌<b>一条都不经过补丁</b>
/// （只有没被内联的少数调用点会命中，比如 mod 自己的代码）。写字段则任何调用点（内联与否）都看得见。
/// </para>
/// <para>
/// <b>动画为什么跟着对</b>：本体 <c>CardPileCmd.GetTweenForCardsChangingPiles</c> 用
/// <c>result.oldPile.Type</c> 挑表现；牌是从 <c>Type == Discard</c> 的那张堆被取进手牌 ⇒ 起点取
/// <c>PileType.Discard.GetTargetPosition</c> ⇒ 就是原版"从弃牌堆取牌"那条动画，不需要另做特效。
/// </para>
/// <para>
/// <b>激活条件</b>：连续判定 —— 每次都现问"物理弃牌堆里还有没有乱流"，不锁存；离开即失效。
/// </para>
/// <para>
/// <b>抽取窗口（2026-10-14 追加）</b>：本体抽牌期间（<c>CardPileCmd.Draw</c> 的整个 Task：
/// <see cref="EnterDraw" /> → <see cref="TrackDraw" />）<b>冻结状态</b>，抽完再判定。
/// 原因：<c>DrawInternal</c> 用局部变量抱住"抽哪张堆"，却每抽一张都重新按类型取堆 ⇒ 抽到最后一张时
/// 切换状态会错位（不触发洗牌 + 断抽）。
/// </para>
/// <para>
/// <b>兜底</b>：字段名属于编译器生成物，本体大版本升级可能改 ⇒ 找不到 / 写不进去时自动退回
/// "挂属性 getter 做替换"（覆盖没被内联的调用点）并打一行日志。两条路由
/// <c>PilePair.FieldSwapWorks</c> 二选一，<b>绝不叠加</b>（叠加就是"换两次 = 换回来"）。
/// </para>
/// </remarks>
internal static class RiotSwap
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
            NoteStateChange(state, active: false);
            return pile;
        }

        return pile.Type switch
        {
            PileType.Draw => ReferenceEquals(pile, pair.Draw) ? LogAndReturn(state, pair.Discard, pile) : pile,
            PileType.Discard => ReferenceEquals(pile, pair.Discard) ? LogAndReturn(state, pair.Draw, pile) : pile,
            _ => pile,
        };
    }

    /// <summary>
    /// 按"当前该不该对调"落地 / 撤销（幂等）。
    /// 字段方案：把 <c>DrawPile</c> / <c>DiscardPile</c> 的属性字段交换 / 还原。
    /// </summary>
    internal static void SyncSwap(PlayerCombatState state, PilePair pair)
    {
        if (Suppressed)
        {
            return;
        }

        var active = IsActive(state);
        if (pair.Swapped == active)
        {
            NoteStateChange(state, active);
            return;
        }

        // 抽取过程中不改状态：本体 DrawInternal 用局部变量抓住"抽哪张堆"，但每抽一张都会重新
        // ShuffleIfNecessary 读一次属性 ⇒ 中途切换会让"局部抱住的堆"和"属性读到的堆"错位，
        // 表现就是最后一张被抽走时不触发洗牌、然后断抽。⇒ 攒到这次抽牌结束再判定。
        if (pair.DrawDepth > 0)
        {
            pair.PendingSync = true;

            if (!pair.DeferredLogged && DeferLogCount < DeferLogLimit)
            {
                pair.DeferredLogged = true;
                DeferLogCount++;
                Const.Logger.Info(
                    $"[{Const.ModId}] [乱流] 抽取中 ⇒ 冻结状态（本次抽牌期间不切换，抽完再判定）");
            }

            return;
        }

        pair.Swapped = active;

        if (pair.CanWriteFields)
        {
            try
            {
                DrawField!.SetValue(state, active ? pair.Discard : pair.Draw);
                DiscardField!.SetValue(state, active ? pair.Draw : pair.Discard);

                // AllPiles 是懒建的，缓存里还留着旧顺序 ⇒ 置空让它按新属性重建。
                AllPilesField?.SetValue(state, null);

                if (!SwapModeLogged)
                {
                    SwapModeLogged = true;
                    Const.Logger.Info(
                        $"[{Const.ModId}] [乱流] 收口方式：属性字段对调（{DrawField.Name} ⇄ {DiscardField.Name}，内联调用点也生效）");
                }
            }
            catch (Exception ex)
            {
                // 退回 getter 替换：先把可能只写了一半的字段还原回物理对象。
                pair.FieldSwapWorks = false;
                pair.Swapped = false;
                RestoreFields(state, pair);
                Const.Logger.Warn(
                    $"[{Const.ModId}] [乱流] 属性字段写不进去，改用 getter 替换兜底：{ex.GetType().Name}: {ex.Message}");
            }
        }

        NoteStateChange(state, active);
    }

    private static void RestoreFields(PlayerCombatState state, PilePair pair)
    {
        try
        {
            DrawField?.SetValue(state, pair.Draw);
            DiscardField?.SetValue(state, pair.Discard);
            AllPilesField?.SetValue(state, null);
        }
        catch
        {
            // 还原失败也无所谓：字段方案已被永久关闭，游戏读到的就是原始对象。
        }
    }

    private static FieldInfo? FindPileField(string propertyName)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var direct = typeof(PlayerCombatState).GetField($"<{propertyName}>k__BackingField", Flags);
        if (direct is not null)
        {
            return direct;
        }

        // 编译器生成物名字万一变了：退一步按"类型是 CardPile + 名字里带属性名"找。
        return typeof(PlayerCombatState)
            .GetFields(Flags)
            .FirstOrDefault(f => f.FieldType == typeof(CardPile)
                && f.Name.Contains(propertyName, StringComparison.Ordinal));
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

    internal static CardPile LogAndReturn(PlayerCombatState state, CardPile redirected, CardPile original)
    {
        RedirectHits++;
        NoteStateChange(state, active: true);

        var now = Godot.Time.GetTicksMsec();
        if (now - LastRedirectLogMs >= 3000)
        {
            LastRedirectLogMs = now;
            Const.Logger.Info(
                $"[{Const.ModId}] [乱流] getter 替换生效：{original.Type} → {redirected.Type}（累计 {RedirectHits} 次）");
        }

        return redirected;
    }

    /// <summary>状态切换时打一行（点亮 / 失效），只在真的变了的时候打。</summary>
    internal static void NoteStateChange(PlayerCombatState state, bool active)
    {
        if (LastActive.TryGetValue(state, out var last) && last == active)
        {
            return;
        }

        var firstTime = !LastActive.ContainsKey(state);
        LastActive[state] = active;

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

    internal static void CachePiles(PlayerCombatState state)
    {
        if (Piles.TryGetValue(state, out _))
        {
            return;
        }

        // 抓"真身"时必须把对调关掉：属性可能已被换过 ⇒ 两张堆的名字会记反。
        Suppressed = true;
        try
        {
            Piles.Add(state, new PilePair
            {
                Draw = state.DrawPile,
                Discard = state.DiscardPile,
                Hand = state.Hand,
            });
        }
        finally
        {
            Suppressed = false;
        }
    }

}
