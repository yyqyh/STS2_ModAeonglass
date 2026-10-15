using HarmonyLib;

using Godot;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Presentation.Combat;
using STS2_Aeonglass.Core.Systems;
using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Presentation.Support;

using STS2_Aeonglass.Core.Systems.Gap;
namespace STS2_Aeonglass.Core.Presentation.Combat.Gap;
/// <summary>时空裂隙 动画流水线的<b>对外入口</b>：<c>CollapseAsync</c>（坍缩）/ <c>RecallAsync</c>（唤回）。</summary>
/// <remarks>
/// ① 职责：编排"手牌 → 传送门 → 间隙 / 间隙 → 手牌"的整段演出（本文件放坍缩主流程）。
/// ② 调用时机：按键 / 控制面板 / 卡牌触发的坍缩·回归（<c>GapStore</c> 决定"能不能做"）。
/// ③ 依赖：<c>AeonglassGapPortals</c> + 额外手牌容器 + 本体卡牌飞行特效。
/// ④ ⚠️ 只演不改状态（状态由 <c>GapStore</c> 收）；分部见 <c>.Recall.cs</c> / <c>.Support.cs</c>（同一个 partial 类）。
/// </remarks>

internal static partial class AeonglassGapSequence
{
    // ==================== 可调参数（改这里就行） ====================

    /// <summary>抬起高度（像素，向上）。</summary>
    private const float LiftHeight = 150f;

    /// <summary>抬起用时（秒）。</summary>
    private const float LiftDuration = 0.16f;

    /// <summary>每张牌之间的错开时间（秒）—— 让它们鱼贯而入，而不是糊成一坨。</summary>
    private const float Stagger = 0.045f;

    /// <summary>抬起之后、起飞之前的停顿（秒）—— 用来等门张开。</summary>
    private const float FlyHold = 0.08f;

    /// <summary>开门用时（秒）。</summary>
    private const float OpenDuration = 0.20f;

    /// <summary>关门用时（秒）。</summary>
    private const float CloseDuration = 0.22f;

    /// <summary>坍缩吸进哪一道门（true = 右门）。</summary>
    private const bool FlyRight = true;

    /// <summary>"粒子已经到落点"的判定阈值（像素）。</summary>
    private const float ArriveEpsilon = 2f;

    /// <summary>等粒子到落点的超时（秒）——本体特效最长约 1.5 秒，给足余量；超时也照常往下走。</summary>
    private const float FlyArrivalTimeout = 3f;

    // ===== 回归（用户 2026-10-12 口径：间隙牌透明化消失 → 左门开启 → 粒子移向中央 →
    //       粒子变成卡牌 → 丢弃手牌 → 卡牌从上方进入手牌）=====

    /// <summary>间隙里的卡面"透明化消失"用时（秒）。</summary>
    private const float RecallFadeDuration = 0.26f;

    /// <summary>中央"粒子聚成卡牌"用时（秒）。</summary>
    private const float RecallFormDuration = 0.22f;

    /// <summary>多张牌在中央的横向间距（像素）。</summary>
    private const float RecallFormSpacing = 92f;

    /// <summary>
    /// "下移进入手牌"的目标点：**相对手牌区中心**的偏移（默认 (0,0) = 手牌区中心）。
    /// </summary>
    /// <remarks>
    /// 卡面从 <see cref="RecallFormOffset" /> 那条线飞到这里，再由手牌容器接手、精确落进它自己的槽位。
    /// 想改"从哪儿下来 / 下到哪儿"就调这两个偏移。
    /// </remarks>
    private static readonly Vector2 RecallDropOffset = new(0, 440);

    /// <summary>卡牌从中央落进手牌用时（秒）。</summary>
    private const float RecallDropDuration = 0.34f;

    /// <summary>
    /// 「粒子变成卡牌」那条线：**相对手牌区中心的偏移**（负 = 在手牌上方）。
    /// </summary>
    /// <remarks>
    /// 用户 2026-10-12：「移动到手牌上方（同坍缩上移处）」——坍缩的抬起是各张牌在**自己原位**上抬
    /// <see cref="LiftHeight" />，回归没有"原位"可言，所以这里按手牌区中心给一条基准线。
    /// 觉得偏上/偏下/偏左偏右就改这个；多张牌之间的横向间距看 <see cref="RecallFormSpacing" />。
    /// </remarks>
    private static readonly Vector2 RecallFormOffset = new(0f, 300f);

    /// <summary>回归从哪一道门出来（true = 左门）。</summary>
    private const bool RecallFromLeft = true;

    // ================================================================

    /// <summary>本体的"卡牌飞行特效"落点字段（<c>NCardFlyVfx._endPos</c>）——只解析一次。</summary>
    private static AccessTools.FieldRef<NCardFlyVfx, Vector2>? _flyEndPos;
    private static bool _flyEndPosResolved;

    /// <summary>一小张牌在演出期间的"旅行状态"。</summary>
    private readonly record struct StagedCard(CardModel Card, NCard Node, Vector2 Start, Vector2 StartScale);

    /// <summary>
    /// 「坍缩」的完整入口（机制 + 表现）：**必然失败时什么都不演**，直接把机制的失败原因带回去。
    /// </summary>
    internal static async Task<GapResult> CollapseAsync(Player player)
    {
        // ① 预测 + 找门：任意一项拿不到就退回本体表现（机制照常能跑）
        var plan = GapCollapseActions.PlanCollapse(player);
        if (plan.Count == 0 || OperationBarrier.IsBusy)
        {
            return await GapCollapseActions.Activate(player);
        }

        if (AeonglassGapPortals.Instance is not { } portals
            || !portals.TryGetPortalCenter(FlyRight, out var portalCenter))
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 传送门不可用 ⇒ 坍缩退回本体表现");
            return await GapCollapseActions.Activate(player);
        }

        // ② 抓卡节点（**必须在机制改模型之前** —— 模型一离开手牌，手牌立刻重排）
        var staged = StageHandCards(plan);
        if (staged.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 没抓到卡节点 ⇒ 坍缩退回本体表现");
            return await GapCollapseActions.Activate(player);
        }

        using var barrier = OperationBarrier.Enter("M2坍缩");
        try
        {
            // ⓪ 用户口径：坍缩**先清间隙** —— 先把旧的间隙卡面淡出（存储层由机制端清），
            //    和抬起同时演，不额外拖时间。
            var stale = NCombatRoom.Instance?.Ui is { } combatUi
                ? StageGapViewCards(combatUi, player)
                : [];
            if (stale.Count > 0)
            {
                _ = TaskHelper.RunSafely(FadeGapCardsAsync(stale)); // 和抬起并行演，不等它
            }

            // ③ 机制层搬家（镜像先不挂，等卡面真的进门）
            var result = await GapCollapseActions.Activate(player, deferMirror: true);
            if (!result.Ok)
            {
                // 没搬成 ⇒ 把卡节点还回手牌，别让玩家凭空少几张牌
                ReturnToHand(staged);
                CombatUiRefresh.RefreshAll(player);
                return result;
            }

            // ④ 演出（登记进 VFX 屏障：动画期间 回溯会等它 —— 否则会和正在飞的卡节点抢）
            await VfxBarrier.Track("m2Collapse", PlayAnimAsync(player, portals, staged, portalCenter));
            return result;
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Gap} 坍缩动画抛异常（机制已完成，动画被丢弃）："
                + $"{ex.GetType().Name}: {ex.Message}");

            // 兜底：动画可能死在"挂镜像"之前 ⇒ 这里补上，别留下"间隙里没有卡面"这种半截状态。
            try
            {
                await GapViewPile.SyncAsync(player);
                CombatUiRefresh.RefreshAll(player);
            }
            catch (Exception inner)
            {
                Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Gap} 兜底补同步也失败了：{inner.Message}");
            }

            return GapResult.Success("坍缩（动画异常，机制已完成）");
        }
        finally
        {
            // 无论成功、失败还是抛异常，门都必须合上（否则会卡在"开着"的状态里）
            await ClosePortalQuietlyAsync();
        }
    }

}
