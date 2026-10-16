using Godot;

using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

using STS2_Aeonglass.Core.Presentation.Art;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;

/// <summary>卡面的<b>悬停表现</b>：金色背光 + 放大 + 抬起 + 轻微倾斜 + tips（<c>AeonglassIntentCardView</c> 的 partial）。</summary>
/// <remarks>
/// ① 职责：<c>EnsureHoverBacklight</c> / <c>SetHovered</c> / <c>ShowHoverTips</c> / <c>CurrentFactor</c> / <c>CurrentTiltDeg</c> / <c>AnimateToTarget</c>。
/// ② 调用时机：槽位 <c>MouseEntered/Exited</c>、<c>SetSlot</c>、<c>SetHighlight</c>。
/// ③ 依赖：本体 <c>%Highlight</c>（复制一层做金色背光，材质**不深拷**）+ <c>NHoverTipSet</c>。
/// ④ ⚠️ 时长/倍率/抬起/倾角常量在主页面的"悬停表现"块里（同一个 partial 类）。
/// </remarks>

internal sealed partial class AeonglassIntentCardView
{

    // ==================== 悬停：金色背光 + 放大 + 抬起 + 轻微倾斜 ====================

    /// <summary>
    /// 造一层金色背光：**复制本体那个 <c>%Highlight</c> 节点**（自带 shader 与 AnimShow/AnimHide），
    /// 并给它一份**独立但不深拷**的 <c>ShaderMaterial</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ① 必须在<b>第一次悬停时</b>才建（不在创建卡视图时建）：复制材质 + 首帧上屏的开销挪出"进战斗"那一帧，
    /// 进战斗不再因为这条装饰而卡一下。
    /// </para>
    /// <para>
    /// ② <c>Duplicate()</c> 用**默认参数**（不深拷子资源）：材质实例独立 ⇒ 两层的 <c>width</c> 参数互不干扰，
    /// 但 <c>Shader</c> 本体共享 ⇒ 不会给每张卡多编译一份 shader 管线（`Duplicate(true)` 会，那正是卡顿的来源之一）。
    /// </para>
    /// </remarks>
    private void EnsureHoverBacklight()
    {
        if (_hoverLayer is { } existing && GodotObject.IsInstanceValid(existing))
        {
            return;
        }

        if (_card.CardHighlight is not { } source || source.GetParent() is not { } parent)
        {
            return;
        }

        try
        {
            var layer = (NCardHighlight)source.Duplicate();
            if (layer.Material is ShaderMaterial shared)
            {
                layer.Material = (ShaderMaterial)shared.Duplicate();
            }

            layer.Name = "HoverBacklight";
            layer.Modulate = NCardHighlight.gold;
            layer.MouseFilter = Control.MouseFilterEnum.Ignore;

            parent.AddChildSafely(layer);
            parent.MoveChild(layer, Math.Min(source.GetIndex() + 1, parent.GetChildCount() - 1));
            layer.AnimHideInstantly();

            _hoverLayer = layer;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 悬停背光层创建失败（只影响悬停观感）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>槽位序号（每行重排后由卡带告知，用于决定倾斜方向）。</summary>
    internal void SetSlot(int index, int count)
    {
        _slotIndex = index;
        _slotCount = Math.Max(1, count);

        if (_hovered)
        {
            AnimateToTarget();
        }
    }

    /// <summary>
    /// 悬停开 / 关：金色背光（淡入淡出）+ 放大 + 抬起 + 轻微倾斜，全部走**同一条 Tween**。
    /// </summary>
    /// <remarks>
    /// 三档语义互不覆盖：红（将打出）/ 青（我们选中）走本体的 <c>%Highlight</c> 原层，
    /// 悬停走这里复制出来的金色层 —— 所以"将打出 + 悬停"两处光可以同时亮着。
    /// </remarks>
    internal void SetHovered(bool hovered)
    {

        if (!IsAlive || _hovered == hovered)
        {
            return;
        }

        _hovered = hovered;
        _wrapper.ZIndex = hovered ? HoverZIndex : 0;

        // 背光层是**第一次悬停才建**的（把复制材质/首次上屏的成本挪出"进战斗"那一帧）。
        if (hovered)
        {
            // 尺寸可能是"创建那一刻还没量到、后来才到位"的 ⇒ 悬停前补一次贴合（解析式，幂等）。
            if (!_fitted)
            {
                ResizeTo(_slotSize);
            }

            // 卡面尺寸/位置随时可能被本体重排 ⇒ 悬停前再实测对齐一次（已对齐时残差≈0，等于没动）。
            AlignArtToSlot();
            EnsureHoverBacklight();
        }

        if (_hoverLayer is { } layer && GodotObject.IsInstanceValid(layer))
        {
            if (hovered)
            {
                layer.AnimShow();
            }
            else
            {
                layer.AnimHide();
            }
        }

        if (hovered)
        {
            ShowHoverTips();
        }
        else
        {
            NHoverTipSet.Remove(_wrapper);
        }

        AnimateToTarget();
    }

    /// <summary>悬停时弹卡面自带的 tips（模型没有 tips 就不弹；不新增本地化键）。</summary>
    private void ShowHoverTips()
    {
        try
        {
            var tips = _card.Model?.HoverTips;
            if (tips is null || !tips.Any())
            {
                return;
            }

            NHoverTipSet.CreateAndShow(_wrapper, tips);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Intent} 意图 tips 失败（已忽略）：{ex.GetType().Name}");
        }
    }

    /// <summary>当前应有的放大倍率：强调（红/青）与悬停取较大者，两者同时命中再叠一点。</summary>
    private float CurrentFactor()
    {
        // ⚠️ "放大 20%"只给**有指向意义的那张**（红 = 将打出 / 青 = 我们选中）；
        //    发牌结束后的 Idle（非选中待命）只点青色背光、不放大，否则整条卡带会一起变大。
        var emphasis = _highlight is IntentHighlight.Pending or IntentHighlight.Selected;
        return (emphasis, _hovered) switch
        {
            (true, true) => HoverPlusEmphasisFactor,
            (true, false) => EmphasisScale,
            (false, true) => HoverFactor,
            _ => 1f,
        };
    }

    /// <summary>悬停倾斜：左半往左倒、右半往右倒（像摊开的手牌）。</summary>
    private float CurrentTiltDeg()
    {
        if (!_hovered || _slotCount <= 1)
        {
            return 0f;
        }

        var middle = (_slotCount - 1) * 0.5f;
        var offset = _slotIndex - middle;
        return Mathf.Sign(offset) * HoverTiltDeg;
    }

    /// <summary>把"缩放 / 抬起 / 倾斜"三个目标值一次性补间过去（进快出慢）。</summary>
    private void AnimateToTarget()
    {
        if (!IsAlive)
        {
            return;
        }

        var factor = CurrentFactor();
        var targetScale = _baseScale * factor * RevealScale();
        // 同样：轴心在中心 ⇒ 位置用解析式基准，抬起是额外的一点点位移。
        var targetPosition = _basePosition + RevealOffset() + new Vector2(0f, _hovered ? -HoverLift : 0f);
        var targetRotation = Mathf.DegToRad(CurrentTiltDeg());
        var seconds = _hovered ? HoverInSeconds : HoverOutSeconds;

        _visualTween?.Kill();
        _visualTween = _wrapper.CreateTween().SetParallel();
        _visualTween.TweenProperty(_card, "scale", targetScale, seconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        _visualTween.TweenProperty(_card, "position", targetPosition, seconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        _visualTween.TweenProperty(_card, "rotation", targetRotation, seconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
    }

}

