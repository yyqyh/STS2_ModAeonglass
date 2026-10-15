using System.Collections.Generic;

using Godot;

using HarmonyLib;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.TopBar;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib.CardPiles;
using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Presentation.Combat;
using STS2_Aeonglass.Core.Systems;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Persistence;

using STS2_Aeonglass.Core.Systems.Bootstrap;
namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// 顶栏两颗「牌组」按钮的<b>悬浮提示互换</b>（双卡组）。
/// </summary>
/// <remarks>
/// <para>
/// <b>要解决的问题</b>：双卡组 是"内容互换"语义 —— 生效中的那副永远真实躺在 <c>player.Deck</c> 里，
/// 所以顶栏<b>本体「卡组」按钮里装的可能是任意一副</b>，而副卡组按钮里装的是另一副。
/// 两颗按钮的默认提示一个是本体的 <c>DECK.*</c>、一个是我们注册牌堆时的固定文案 ⇒
/// 切换之后两张提示就"名不副实"了。
/// </para>
/// <para>
/// <b>做法</b>：在 <see cref="NHoverTipSet.CreateAndShow(Control, IEnumerable{IHoverTip}, HoverTipAlignment)" />
/// 上打一个 prefix，把 <c>hoverTips</c> 换成按<b>当前状态</b>算出来的那一条：
/// <list type="bullet">
///   <item><description>本体卡组按钮 ⇒ 显示"**生效中**那一副"的名字（索引 = <see cref="ActiveDeckProvider.ActiveIndexOf" />）；</description></item>
///   <item><description>副卡组按钮 ⇒ 显示"**不在生效**那一副"的名字（索引 = 1 − 生效索引）。</description></item>
/// </list>
/// 因为是在"悬浮那一刻"算的，<b>每次切换后都自动是对的</b>，不需要在切换处再刷新一次。
/// </para>
/// <para>
/// <b>边界</b>：只作用于本角色（<see cref="CharacterGate" />）与这两颗按钮；文案全在我们自己的表里
/// （<c>static_hover_tips</c> 的 <c>STS2_AEONGLASS_DECKTIP_*</c>），改文案不用动代码。
/// 不碰本体的 <c>DECK.*</c> 全局键，所以别的角色一点不受影响。
/// </para>
/// <para>
/// ⚠️ <b>异常兜底（2026-10-15 补）</b>：这是<b>全局悬浮提示</b>的入口（全角色、全时刻都在走）⇒
/// 整流包 try/catch，异常时 <c>hoverTips</c> 保持本体原值、只记日志。
/// </para>
/// </remarks>
[HarmonyPatch(
    typeof(NHoverTipSet),
    nameof(NHoverTipSet.CreateAndShow),
    [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class DeckTipSwapPatch
{
    /// <summary>原卡组（A）的文案词根。</summary>
    private const string PrimaryStem = "STS2_AEONGLASS_DECKTIP_PRIMARY";

    /// <summary>副卡组（B）的文案词根。</summary>
    private const string SecondaryStem = "STS2_AEONGLASS_DECKTIP_SECONDARY";

    [HarmonyPrefix]
    private static void Prefix(Control __0, ref IEnumerable<IHoverTip> __1)
    {
        try
        {
            // 只作用于沙漏；别的角色/别的 mod 一律原样放行。
            if (!CharacterGate.IsActive || !DeckModule.Enabled)
            {
                return;
            }

            if (PlayerOfDeckButtons() is not { } player)
            {
                return;
            }

            var activeIndex = ActiveDeckProvider.ActiveIndexOf(player);

            // 先判断"这是不是我们那两颗按钮之一"；顺带兜底刷一次图标
            //（图标主要在切换成功时刷，这里只是防止 RitsuLib / 本体在别处把它覆盖回去）。
            var isVanillaDeck = __0 is NTopBarDeckButton;
            var isModDeckB = __0 is NModCardPileButton { Definition: { } def }
                             && DeckPiles.DeckBPileType is { } expected
                             && def.PileType == expected;

            if (!isVanillaDeck && !isModDeckB)
            {
                return;
            }

            DeckButtonIcons.Refresh(player);

            // 赋值放在最后一句：中途出问题 ⇒ hoverTips 还是本体原值，不留半成品。
            if (isVanillaDeck)
            {
                // 本体「卡组」按钮：里面永远是**生效中**那副。
                __1 = [BuildTip(deckIndex: activeIndex, isActive: true)];
                return;
            }

            // 副卡组按钮：里面是**不在生效**那副。
            __1 = [BuildTip(deckIndex: 1 - activeIndex, isActive: false)];
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 卡组按钮提示改写失败（原样放行）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>按"哪一副 + 是否生效"取我们的文案造一条提示。</summary>
    private static HoverTip BuildTip(int deckIndex, bool isActive)
    {
        // deckIndex：0 = 原卡组（A），1 = 副卡组（B）；与 DeckPiles.NameOf 同口径。
        var stem = deckIndex == 1 ? SecondaryStem : PrimaryStem;
        var stateKey = isActive ? ".active" : ".inactive";

        return new HoverTip(
            new LocString("static_hover_tips", stem + ".title"),
            new LocString("static_hover_tips", stem + stateKey));
    }

    /// <summary>当前本地玩家（战斗内问战斗状态，战斗外问跑局状态）。</summary>
    private static Player? PlayerOfDeckButtons() =>
        LocalContext.GetMe(CombatManager.Instance?.DebugOnlyGetState())
        ?? LocalContext.GetMe(RunManager.Instance?.DebugOnlyGetState());
}
