using Godot;

using MegaCrit.Sts2.Core.Nodes.Cards;

using STS2_Aeonglass.Core.Presentation.Art;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;

/// <summary>发牌特效在卡面上的那两下：**显现进度**与**到位的红闪 / 顶一下**（<c>AeonglassIntentCardView</c> 的 partial）。</summary>
/// <remarks>
/// ① 职责：<c>SetReveal</c>（透明度 + 上浮，直接应用不走补间）/ <c>PlayArrivalFlash</c> / <c>PlayEmphasisPop</c>。
/// ② 调用时机：<c>AeonglassIntentBelt.Deal</c>（发牌序列）与卡带收尾。
/// ③ 依赖：本体 <c>%Highlight.AnimFlash</c> + <c>AeonglassUiPalette</c>。
/// ④ ⚠️ 显现的**上浮偏移**也参与 <c>ApplyScale</c>/<c>AnimateToTarget</c> 的位置表达式（在主页面的定位那段）。
/// </remarks>

internal sealed partial class AeonglassIntentCardView
{

    // ==================== 发牌特效（`AeonglassIntentBelt.Deal` 驱动）====================

    /// <summary>发牌显现进度：0 = 还没显现（全透明 + 在下方 <see cref="DealRevealRise" /> 处），1 = 完全显现。</summary>
    private float _reveal = 1f;

    /// <summary>显现时从下方多少（槽位本地单位）上浮到位。</summary>
    private const float DealRevealRise = 6f;

    /// <summary>
    /// 显现时的起始缩放：`1` = 不缩放（定稿口径：透明度 + 上浮）；
    /// 想让卡面"**从 0% 放大到 100%**"就把这里改成 <c>0f</c>（会与上浮叠加）。
    /// </summary>
    private const float DealRevealScaleFrom = 1f;

    /// <summary>当前显现进度带来的位置偏移（完全显现 = 0；未显现 = 下移 <see cref="DealRevealRise" />）。</summary>
    private Vector2 RevealOffset() => new(0f, (1f - _reveal) * DealRevealRise);

    /// <summary>当前显现进度带来的缩放系数（默认恒为 1）。</summary>
    private float RevealScale() => Mathf.Lerp(DealRevealScaleFrom, 1f, _reveal);

    /// <summary>
    /// 发牌特效用：设置显现进度（**直接应用**，不走补间 —— 进度由卡带的特效每帧算出来）。
    /// </summary>
    internal void SetReveal(float t)
    {
        if (!IsAlive)
        {
            return;
        }

        _reveal = Mathf.Clamp(t, 0f, 1f);
        _card.Modulate = new Color(1f, 1f, 1f, _reveal);
        ApplyScale(CurrentFactor());
    }

    /// <summary>
    /// 发牌到位 / "怪物选中的那张"变大时的**红闪**：直接用本体 <c>%Highlight</c> 自带的 <c>AnimFlash</c> + 本体红色。
    /// </summary>
    internal void PlayArrivalFlash()
    {
        if (!IsAlive)
        {
            return;
        }

        try
        {
            _card.CardHighlight.Modulate = NCardHighlight.red;
            _card.CardHighlight.AnimFlash();
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 发牌红闪失败（只影响观感）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>"变大"那一下：把槽位整体放大再回位（槽位缩放 = 卡面与判定一起放大，两边仍然一致）。</summary>
    internal void PlayEmphasisPop(float peak = 1.15f, float seconds = 0.4f)
    {
        if (!IsAlive)
        {
            return;
        }

        _wrapper.Scale = Vector2.One;
        var tween = _wrapper.CreateTween();
        tween.TweenProperty(_wrapper, "scale", Vector2.One * peak, seconds * 0.4f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_wrapper, "scale", Vector2.One, seconds * 0.6f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
    }
}

