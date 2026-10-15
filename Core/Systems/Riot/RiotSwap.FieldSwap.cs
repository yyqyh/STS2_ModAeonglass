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
/// 乱流的<b>对调落地</b>：把两张属性字段真的换过来 / 换回去（<c>RiotSwap</c> 的分册）。
/// </summary>
/// <remarks>
/// ① 职责：<c>SyncSwap</c>（按"该不该对调"落地/撤销 + 抽取窗口冻结判定）与 <c>RestoreFields</c>（兜底还原）。
/// ② 调用时机：牌堆事件、回合开始、读属性时的保险丝；抽牌窗口内推迟。
/// ③ 依赖：<c>RiotSwap</c> 的共享状态（真身缓存、字段句柄）。
/// ④ ⚠️ 只改字段、不动 UI；写失败要退回落点并清掉可能写了一半的字段。
/// </remarks>
internal static partial class RiotSwap
{

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
            RiotSwapLog.NoteStateChange(state, active);
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
                    $"[{Const.ModId}] {Const.Prefix.Riot} 抽取中 ⇒ 冻结状态（本次抽牌期间不切换，抽完再判定）");
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
                        $"[{Const.ModId}] {Const.Prefix.Riot} 收口方式：属性字段对调（{DrawField.Name} ⇄ {DiscardField.Name}，内联调用点也生效）");
                }
            }
            catch (Exception ex)
            {
                // 退回 getter 替换：先把可能只写了一半的字段还原回物理对象。
                pair.FieldSwapWorks = false;
                pair.Swapped = false;
                RestoreFields(state, pair);
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Riot} 属性字段写不进去，改用 getter 替换兜底：{ex.GetType().Name}: {ex.Message}");
            }
        }

        RiotSwapLog.NoteStateChange(state, active);
    }

    internal static void RestoreFields(PlayerCombatState state, PilePair pair)
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
}
