using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// 「我们的操作改动了牌之后，立刻把相关 UI 刷一遍」的**统一入口**。
/// </summary>
/// <remarks>
/// <para>
/// 为什么需要它（2026-10-12 用户口径）：我们的机制大量使用**静默搬运**（`RemoveInternal`/`AddInternal`，
/// 不走 `CardPileCmd`）⇒ 本体的 UI 不会自己刷新。而**不同的 UI 订阅的事件不一样**：
/// </para>
/// <list type="bullet">
///   <item><description>
///   顶栏「牌组」按钮（<c>NTopBarDeckButton</c>）听的是 <c>CardAddFinished</c> / <c>CardRemoveFinished</c>
///   —— <b>不听 <c>ContentsChanged</c></b>！只发 ContentsChanged 的话**牌组数量不会变**（实测踩过）。
///   </description></item>
///   <item><description>牌堆界面（<c>NDeckViewScreen</c> / <c>NCardPileScreen</c>）与联机卡表听 `ContentsChanged`。</description></item>
/// </list>
/// <para>
/// 所以这里对每个相关牌堆**三个事件都发**：<c>ContentsChanged</c> + <c>InvokeCardAddFinished</c> +
/// <c>InvokeCardRemoveFinished</c>。三个都是幂等的"通知"，重复发只是多刷一次显示。
/// </para>
/// <para>
/// 覆盖范围：**牌组**（卡组层）+ 战斗里的五堆（手牌/抽/弃/消耗/出牌）+ **额外手牌容器（间隙）**。
/// </para>
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
        RefreshZone(TimeGap.Zone(player));                       // M2 间隙·存储（Headless，无 UI，但保持事件一致）
        RefreshZone(GapView.Zone(player));                       // M2 间隙·显示（额外手牌容器）
        RefreshZone(StasisZone.Zone(player));                    // M7 搁浅（顶栏按钮计数）
        RefreshZone(DoubleDeckService.InactivePileOf(player));   // M6 非生效副（顶栏按钮计数）

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
