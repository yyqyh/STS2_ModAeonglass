using Godot;

using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

using STS2RitsuLib.CardPiles;
using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Presentation.Support;

using STS2_Aeonglass.Core.Systems.Stasis;
namespace STS2_Aeonglass.Core.Presentation.Combat;
/// <summary>搁浅「搁浅」顶栏按钮的<b>闪烁提示</b>（卡归队时闪一下）。</summary>
/// <remarks>
/// ① 职责：找到顶栏搁浅按钮并让它闪一次。
/// ② 调用时机：<c>StasisZone</c> 归队时 / 控制面板"搁浅页"的验证按钮。
/// ③ 依赖：按 <c>StasisZone.ZoneId</c> 找 <c>NModCardPileButton</c>（用 <c>NodeSearch</c>）。
/// ④ ⚠️ 找不到按钮 ⇒ <b>静默不闪</b>（不抛错、不影响归队逻辑）。
/// </remarks>

internal static class AeonglassStasisPileFlash
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
                    $"[{Const.ModId}] [搁浅] 归队 {returned} 张，但没找到「搁浅」顶栏按钮 ⇒ 本次不闪（不影响机制）");
                return;
            }

            var tween = button.CreateTween();
            tween.SetLoops(Loops);
            tween.TweenProperty(button, "modulate", FlashColor, HalfPeriod);
            tween.TweenProperty(button, "modulate", Colors.White, HalfPeriod);

            Const.Logger.Info($"[{Const.ModId}] [搁浅] 归队 {returned} 张 → 「搁浅」按钮闪烁 {Loops} 次");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] [搁浅] 闪烁提示失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
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
        return NodeSearch.FindDescendant<NModCardPileButton>(topBar, button => string.Equals(button.Definition?.Id, target, StringComparison.OrdinalIgnoreCase));
    }

}
