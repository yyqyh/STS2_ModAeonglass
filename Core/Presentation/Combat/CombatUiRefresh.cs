using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Gap;
using STS2_Aeonglass.Core.Systems.Stasis;
namespace STS2_Aeonglass.Core.Presentation.Combat;
/// <summary>"我们的操作改动了牌之后，立刻把相关 UI 刷一遍"的<b>统一入口</b>。</summary>
/// <remarks>
/// ① 职责：扇出到各刷新器（手牌 / 额外手牌 / 牌堆按钮 / 意图卡带）。
/// ② 调用时机：时空裂隙 坍缩·回归、回溯、双卡组 切副、搁浅 搁浅改动牌之后由系统层调。
/// ③ 依赖：<c>ExtraHandRefresh</c> / <c>DeckButtonIcons</c> / <c>AeonglassIntentBelt</c> 等。
/// ④ ⚠️ 只刷显示、不动数据；必须可重复调用（幂等），别在这里做机制判断。
/// </remarks>

internal static class CombatUiRefresh
{
    /// <summary>把我们操作波及的所有牌堆 UI 刷一遍（<paramref name="player" /> 为空时什么都不做）。</summary>
    public static void RefreshAll(Player? player)
    {
        if (player is null)
        {
            return;
        }

        // ① 牌组（卡组层）：交换卡组、滞留归队、被滞留时母本离队都会改它。
        Refresh(player.Deck);

        // ② 战斗里的牌堆：间隙操作 / 热切换会动到它们。
        if (player.PlayerCombatState is { } combat)
        {
            foreach (var pile in combat.AllPiles)
            {
                Refresh(pile);
            }
        }

        // ③ **我们自己的三个 run 持久牌堆**（⚠️ 关键补充，2026-10-12 实测漏了这一步）：
        //    它们**既不在 `player.Deck`、也不在战斗 `AllPiles`** —— 是 RitsuLib 的模组牌堆，
        //    但挂着它们自己的 UI：间隙 = 额外手牌容器，搁浅 = 顶栏「搁浅」按钮，另一副 = 顶栏「另一副牌组」按钮。
        //    RitsuLib 的牌堆按钮订阅的是 **ContentsChanged + CardAddFinished + CardRemoveFinished 三个**（源码确认）
        //    ⇒ 三个都要发，它的计数才会动。
        RefreshZone(GapStore.Zone(player));                       // 时空裂隙·存储（Headless，无 UI，但保持事件一致）
        RefreshZone(GapViewPile.Zone(player));                       // 时空裂隙·显示（额外手牌容器）
        RefreshZone(StasisStore.Zone(player));                    // 搁浅 搁浅（顶栏按钮计数）
        RefreshZone(DeckPiles.InactivePileOf(player));   // 双卡组 非生效副（顶栏按钮计数）

        // ④ 额外手牌容器（间隙）：找 RitsuLib 的容器重新同步 + 压层级。
        ExtraHandRefresh.Nudge(player);
    }

    /// <summary>刷一个"模组 run 持久牌堆"（可能为 null：机制关掉 / 未解析）。</summary>
    private static void RefreshZone(CardPile? pile)
    {
        if (pile is not null)
        {
            Refresh(pile);
        }
    }

    /// <summary>单独刷一个牌堆（只在确实需要精调时用；一般直接调 <see cref="RefreshAll" />）。</summary>
    public static void Refresh(CardPile pile)
    {
        pile.InvokeContentsChanged();
        pile.InvokeCardAddFinished();
        pile.InvokeCardRemoveFinished();
    }
}
