using Godot;

using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

using STS2RitsuLib.CardPiles;
using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// M7「搁浅」顶栏按钮的**闪烁提示**（Step 3 第 4 条 / 方案 A）。
/// </summary>
/// <remarks>
/// <para>
/// 触发时机：战斗结束链里**有牌归队**时（由 <c>StasisZone.TickAfterCombatEnd</c> 调 <see cref="Flash" />）。
/// 只在归队时闪 —— 新牌入堆是玩家自己刚打出的，不需要提示。
/// </para>
/// <para>
/// <b>怎么找到那个按钮</b>：RitsuLib 的顶栏牌堆按钮是 <c>NModTopBarPileButton.Create(definition)</c>，
/// 而 <c>NModCardPileButton.Definition</c> 是 <b>public</b> ⇒ 扫 <c>NTopBar</c> 子树、按定义 Id 精确定位。
/// 找不到（版本变了 / 顶栏还没建 / 不是我们的角色）就**静默跳过**，绝不影响机制。
/// </para>
/// <para>纯表现层：只动 <c>Modulate</c>，不碰按钮的输入与状态。</para>
/// </remarks>
internal static class StasisPileFlash
{
    /// <summary>闪烁色（沙漏青）。</summary>
    private static readonly Color FlashColor = new("7FE7DD");

    /// <summary>来回几次 / 每次时长（总时长 ≈ 次数 × 2 × 每次时长）。</summary>
    private const int Loops = 3;
    private const float HalfPeriod = 0.15f;

    /// <summary>闪一下搁浅按钮（<paramref name="returned" /> = 本场归队几张，只用于日志）。</summary>
    public static void Flash(int returned)
    {
        if (returned <= 0 || !AeonglassSettingStore.Current.StasisFlash)
        {
            return;
        }

        try
        {
            var button = FindStasisButton();
            if (button is null)
            {
                Const.Logger.Info(
                    $"[{Const.ModId}] [M7] 归队 {returned} 张，但没找到「搁浅」顶栏按钮 ⇒ 本次不闪（不影响机制）");
                return;
            }

            var tween = button.CreateTween();
            tween.SetLoops(Loops);
            tween.TweenProperty(button, "modulate", FlashColor, HalfPeriod);
            tween.TweenProperty(button, "modulate", Colors.White, HalfPeriod);

            Const.Logger.Info($"[{Const.ModId}] [M7] 归队 {returned} 张 → 「搁浅」按钮闪烁 {Loops} 次");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] [M7] 闪烁提示失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>按定义 Id 在顶栏里找我们的搁浅按钮；找不到返回 null。</summary>
    private static NModCardPileButton? FindStasisButton()
    {
        if (NRun.Instance is not { } run)
        {
            return null;
        }

        var topBar = run.GlobalUi?.TopBar;
        if (topBar is null || !StasisZone.IsReady)
        {
            return null;
        }

        var target = StasisZone.ZoneId;
        return FindIn(topBar, target);
    }

    /// <summary>深度有限地扫子树（顶栏结构不深，限深可以避免扫到无关的大子树）。</summary>
    private static NModCardPileButton? FindIn(Node root, string pileId, int depth = 0)
    {
        if (depth > 6)
        {
            return null;
        }

        foreach (var child in root.GetChildren())
        {
            if (child is NModCardPileButton button
                && string.Equals(button.Definition?.Id, pileId, StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }

            if (FindIn(child, pileId, depth + 1) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
