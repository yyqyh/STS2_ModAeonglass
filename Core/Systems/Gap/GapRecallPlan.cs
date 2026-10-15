using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.AlphaBeta;
using STS2_Aeonglass.Core.Systems.Sand;
using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Systems.Gap;

/// <summary>
/// 回归的<b>预测</b>：算出这次回归能取回哪几张（与执行端同一套规则，只读）。
/// </summary>
/// <remarks>
/// ① 职责：供演出层"演什么"与控制面板预演判可用。
/// ② 调用时机：<c>AeonglassGapSequence</c> 演出前 / 控制面板预演。
/// ③ 依赖：<c>GapStore</c>（存储与容量）+ <c>GapAlternation</c>（交替闸）。
/// ④ ⚠️ 必须与 <c>GapRecallActions.Recall</c> 的判定完全一致。
/// </remarks>
internal static class GapRecallPlan
{
    /// <summary>
    /// <b>预测</b>：这一次「回归」会把间隙里的哪几张搬回手牌（**无任何副作用**）。
    /// </summary>
    /// <remarks>
    /// 返回空 = 这次一定不成功（不在战斗 / 间隙没注册 / 间隙是空的 / 沙砾不够付代价）⇒
    /// 表现层什么都不该演，直接走 <see cref="Recall" /> 拿失败原因。
    /// 与执行端共用"间隙里现在有什么、代价多少"这两条判断，不会出现"演的和做的不一样"。
    /// </remarks>
    public static IReadOnlyList<CardModel> PlanRecall(Player player)
    {
        if (player.PlayerCombatState is null || GapStore.Zone(player) is not { } gap || gap.Cards.Count == 0)
        {
            return [];
        }

        // **与执行端同源**：必须交替（每回合额度已取消）不满足 ⇒ 这一次一定失败 ⇒ 什么都不该演。
        if (!GapAlternation.AllowsAlternation(player, GapAlternation.GapAction.Recall))
        {
            return [];
        }

        // 定价与坍缩同一笔（2026-10-13 统一 2 沙砾，不再按张乘）。
        var cost = GapStore.ActionCost;
        if (cost > 0 && SecondaryResourceCmd.Get(player, SandResourceRegistry.SandId) < cost)
        {
            return [];
        }

        return gap.Cards.ToList();
    }
}
