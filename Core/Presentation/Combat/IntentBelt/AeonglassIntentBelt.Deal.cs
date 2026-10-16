using Godot;

using MegaCrit.Sts2.Core.Helpers;

using STS2_Aeonglass.Core.Presentation.Art;
using STS2_Aeonglass.Core.Presentation.Support;
using STS2_Aeonglass.Core.Systems.Intent;
using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;

/// <summary>
/// 卡带的**发牌（显现）特效**：<see cref="AeonglassIntentBelt" /> 的 partial。
/// </summary>
/// <remarks>
/// <para>
/// 用户 2026-10-15 口径（顺序固定）：
/// ① 初始全空白（没有卡、没有牌堆）；② 卡带**左边**出现琥珀金色粒子（右密左疏、约一小卡宽、粒子往左飘）；
/// ③ 特效**向右加速**移动，开始离开卡带时缓慢淡化、完全离开即消失；
/// ④ 它的右侧进入某张卡的格子时那张卡开始显现，中线离开该格时完全显现，**完全显现的瞬间**卡框闪一次红色细线扩大的特效；
/// ⑤ 红细线 = 卡框大小，扩到 150% 同时淡到透明；
/// ⑥ 粒子消失的那一刻显现**牌堆小卡**，同时"怪物选中的那张意图"变大 + 红闪；⑦ 结束。
/// </para>
/// <para>④ 的"显现"= 透明度 + 缩放（`AeonglassIntentCardView.SetReveal`）；判定区不受影响（判定只认槽位）。</para>
/// </remarks>
public partial class AeonglassIntentBelt
{
    /// <summary>总开关（`static readonly` 而非 `const`：常量会被折叠成"不可达代码"报 CS0162，本项目要求 0 警）。</summary>
    internal static readonly bool PlayDealEffect = true;

    /// <summary>
    /// **触发时机**（留接口）：目前只有 <see cref="CombatStart" /> 一种取值 ——
    /// 语义 = 该卡带在**本场战斗首次出图**时放一次（开场的怪即"进入战斗"；半途召唤的怪也会放）。
    /// 以后要换时机（例如"每次手牌成批变化都放"）就加枚举值 + 在 <c>ShouldPlayDeal</c> 里补分支。
    /// </summary>
    internal enum DealTrigger
    {
        /// <summary>本场战斗首次出图（当前唯一实现）。</summary>
        CombatStart,
    }

    /// <summary>当前触发时机（改这里就换时机）。</summary>
    internal static readonly DealTrigger Trigger = DealTrigger.CombatStart;

    /// <summary>粒子从卡带左外侧扫到右外侧的总时长（秒）。</summary>
    private const float DealSweepSeconds = 1.8f;

    /// <summary>
    /// 扫描的加速指数（"越来越快"）：`p^Power`。
    /// </summary>
    /// <remarks>
    /// `2` = Quad-In、`3` = Cubic-In。4 s 这么长的时候 Cubic 太靠后（前半程几乎不动、卡面挤在最后 1 s 才逐张亮），
    /// 所以定稿用 <b>Quad-In</b>；想回到"更明显的越走越快"就把它改成 3。
    /// </remarks>
    private const float DealSweepPower = 2f;

    /// <summary>粒子数量（"一小卡宽 + 右边很密集"：数量给足，寿命压短、阻尼加大 ⇒ 粒子堆在发射器那一侧）。</summary>
    private const int DealParticleAmount = 160;

    /// <summary>粒子寿命（秒）—— 决定"往左飘"能飘多远；越短 ⇒ 右侧越密、左侧尾巴越短。</summary>
    private const float DealParticleLife = 0.32f;

    /// <summary>粒子往左的初速范围（本地单位/秒）。</summary>
    private const float DealParticleSpeedMin = 26f;
    private const float DealParticleSpeedMax = 70f;

    /// <summary>阻尼（越大越快停下 ⇒ 粒子更贴近发射器那一侧 = 右边更密）。</summary>
    private const float DealParticleDampingMin = 40f;
    private const float DealParticleDampingMax = 90f;

    /// <summary>卡框红闪：颜色 / 线宽 / 扩到多大 / 时长。</summary>
    private const float DealFrameFlashWidth = 1.6f;
    private const float DealFrameFlashScale = 1.5f;
    private const float DealFrameFlashSeconds = 0.5f;

    private bool _dealing;
    private bool _dealt;

    private float _dealTime;
    private float _dealFromX;
    private float _dealToX;
    private float _dealLastRevealX;

    /// <summary>起手那一刻量下来的行宽（`_rowWidth` 要等下一次 <c>SyncPosition</c> 才更新，起手时可能还是旧的/0）。</summary>
    private float _dealRowWidth;
    private Node2D? _dealEmitter;
    private readonly List<AeonglassIntentCardView> _dealFlashed = [];

    /// <summary>卡带重新出图且还没放过发牌特效时，起一次（每只怪每场战斗一次）。</summary>
    private void BeginDealIfNeeded(EnemyIntentState state)
    {
        if (!PlayDealEffect || !ShouldPlayDeal() || _dealt || _dealing || _views.Count == 0)
        {
            return;
        }

        _dealing = true;
        _dealt = true;
        _dealTime = 0f;
        _dealFlashed.Clear();

        var half = SlotWidth * 0.5f;
        // ⚠️ 不能用 `_rowWidth`：它要等下一次 SyncPosition 才更新，起手时还是旧的/0（会让扫描带短一截）。
        var right = MeasureRowWidth();
        _dealRowWidth = right;
        _dealFromX = -half * 1.2f;
        _dealToX = right + half * 1.6f;
        _dealLastRevealX = _dealFromX;

        // ① 初始全空白：卡面显现进度 0、牌堆行先藏起来。
        foreach (var view in _views)
        {
            view.SetReveal(0f);
        }

        _pileRow.Visible = false;
        _dealEmitter = MakeDealEmitter();
        AddChild(_dealEmitter);
        MoveDealEmitter(_dealFromX);

        // 表现层诊断（每场战斗每只怪一行）⇒ 归「详细诊断日志」开关。
        if (AeonglassSettingStore.DebugVerboseLogs)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 发牌特效：monster={state.MonsterId} 卡面 {_views.Count} 张"
                + $"｜扫描 {_dealFromX:0.#} → {_dealToX:0.#}（{DealSweepSeconds:0.##}s，Cubic-In 加速）");
        }
    }

    /// <summary>当前 <see cref="Trigger" /> 是否该放特效（换时机就在这里补分支）。</summary>
    private static bool ShouldPlayDeal() => Trigger switch
    {
        DealTrigger.CombatStart => true,
        _ => false,
    };

    /// <summary>
    /// 量一次行宽给扫描带用：容器可能还没排版（量到 0）⇒ 退回"按槽位数算"的期望宽度。
    /// </summary>
    private float MeasureRowWidth()
    {
        var width = _row.GetCombinedMinimumSize().X;
        if (width > 1f)
        {
            return width;
        }

        return Mathf.Max(SlotWidth, ((SlotWidth + SlotGap) * _views.Count) - SlotGap);
    }

    /// <summary>每帧推进（由 <c>_Process</c> 调）。</summary>
    private void StepDeal(double delta)
    {
        if (!_dealing)
        {
            return;
        }

        _dealTime += (float)delta;
        var p = Mathf.Clamp(_dealTime / DealSweepSeconds, 0f, 1f);

        // ③ 向右加速（越走越快）：p^Power（Power 见 DealSweepPower）。
        var x = Mathf.Lerp(_dealFromX, _dealToX, Mathf.Pow(p, DealSweepPower));
        MoveDealEmitter(x);
        UpdateDealReveal(x);

        if (p >= 1f)
        {
            FinishDeal();
        }
    }

    /// <summary>把扫描位置写进发射器，并按"开始离开卡带"淡化（完全离开 = 完全消失）。</summary>
    private void MoveDealEmitter(float x)
    {
        if (_dealEmitter is null || !GodotObject.IsInstanceValid(_dealEmitter))
        {
            return;
        }

        _dealEmitter.Position = new Vector2(x, SlotHeight * 0.5f);

        var half = SlotWidth * 0.5f;
        var right = _dealRowWidth > 0f ? _dealRowWidth : SlotWidth;
        var fadeFrom = right - half;                       // 特效区开始离开卡带
        var fadeTo = right + half;                         // 完全离开
        var fade = Mathf.Clamp((x - fadeFrom) / Mathf.Max(1f, fadeTo - fadeFrom), 0f, 1f);
        _dealEmitter.Modulate = new Color(1f, 1f, 1f, 1f - fade);
    }

    /// <summary>
    /// ④ 逐张推进显现：它的**右边缘**进入某格时那张开始显现，**中线**离开该格时完全显现（一瞬间触发卡框红闪）。
    /// </summary>
    private void UpdateDealReveal(float x)
    {
        var half = SlotWidth * 0.5f;
        _dealLastRevealX = x;

        foreach (var view in _views)
        {
            if (view.Card is null || !GodotObject.IsInstanceValid(view.Wrapper))
            {
                continue;
            }

            var slotLeft = view.Wrapper.Position.X;
            var slotRight = slotLeft + SlotWidth;
            var start = slotLeft - half;                    // 右边缘刚进入这一格
            var t = Mathf.Clamp((x - start) / Mathf.Max(1f, slotRight - start), 0f, 1f);
            view.SetReveal(t);

            if (t >= 1f && !_dealFlashed.Contains(view))
            {
                _dealFlashed.Add(view);
                SpawnFrameFlash(view);
            }
        }
    }

    /// <summary>收尾：全部显现 + ⑥ 亮牌堆 + "怪物选中的那张"变大红闪。</summary>
    private void FinishDeal()
    {
        if (!_dealing)
        {
            return;
        }

        _dealing = false;

        if (_dealEmitter is { } emitter && GodotObject.IsInstanceValid(emitter))
        {
            emitter.QueueFreeSafely();
        }

        _dealEmitter = null;

        foreach (var view in _views)
        {
            view.SetReveal(1f);
        }

        // ⑥ 粒子消失 ⇒ 显现牌堆小卡，同时把"怪物选中的那张"顶一下 + 红闪。
        _pileRow.Visible = true;
        if (_creature is not null && IntentDeck.TryGet(_creature, out var state) && state.Pending is { } pending)
        {
            foreach (var view in _views)
            {
                if (ReferenceEquals(view.Identity, pending))
                {
                    view.PlayArrivalFlash();
                    view.PlayEmphasisPop();
                    break;
                }
            }
        }

        // 立刻把新档位摊到每一格（否则要等下一次刷新才亮）。
        if (_creature is not null && IntentDeck.TryGet(_creature, out var current))
        {
            foreach (var view in _views)
            {
                if (view.Identity is { } card)
                {
                    view.SetBaseHighlight(HighlightFor(card, current));
                }
            }
        }
    }

    /// <summary>卡带销毁时收尾（只清内容、不做亮牌与"变大"那一下 —— 节点正在被释放）。</summary>
    private void AbortDeal()
    {
        _dealing = false;

        if (_dealEmitter is { } emitter && GodotObject.IsInstanceValid(emitter))
        {
            emitter.QueueFreeSafely();
        }

        _dealEmitter = null;

        foreach (var view in _views)
        {
            view.SetReveal(1f);
        }
    }

    /// <summary>刷新途中卡面被复用/新建时，把"当前扫描位置对应的显现进度"重新摊到每一格上。</summary>
    private void ReapplyDealReveal()
    {
        if (_dealing)
        {
            UpdateDealReveal(_dealLastRevealX);
        }
    }

    // ==================== ⑤ 卡框红闪：红色细线，扩到 150% 同时淡到透明 ====================

    private void SpawnFrameFlash(AeonglassIntentCardView view)
    {
        try
        {
            var slotLeft = view.Wrapper.Position.X;
            var flash = new DealFrameFlash(
                new Vector2(SlotWidth, SlotHeight),
                AeonglassUiPalette.FlashRed,
                DealFrameFlashWidth)
            {
                Name = "DealFrameFlash",
                ZIndex = 60,
                MouseFilter = MouseFilterEnum.Ignore,
            };

            // 以卡框中心为基准放大：自己每帧把 Position 摆到"中心 - 当前尺寸一半"。
            flash.Center = new Vector2(slotLeft + (SlotWidth * 0.5f), SlotHeight * 0.5f);
            AddChild(flash);

            var tween = flash.CreateTween().SetParallel();
            tween.TweenProperty(flash, "size", new Vector2(SlotWidth, SlotHeight) * DealFrameFlashScale, DealFrameFlashSeconds)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(flash, "modulate:a", 0f, DealFrameFlashSeconds)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            tween.Chain().TweenCallback(Callable.From(() => flash.QueueFreeSafely()));
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 发牌卡框红闪失败（只影响观感）：{ex.GetType().Name}");
        }
    }

    /// <summary>红色细线卡框：尺寸可补间，位置始终以 <see cref="Center" /> 为基准（缩放绕中心）。</summary>
    private sealed partial class DealFrameFlash : Control
    {
        private readonly Color _color;
        private readonly float _width;

        internal DealFrameFlash(Vector2 size, Color color, float width)
        {
            Size = size;
            _color = color;
            _width = width;
        }

        internal Vector2 Center { get; set; }

        public override void _Process(double delta)
        {
            Position = Center - (Size * 0.5f);
            QueueRedraw();
        }

        public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), _color, filled: false, width: _width);
    }

    // ==================== ②③ 粒子：琥珀金色、右密左疏、往左飘 ====================

    /// <summary>
    /// 造扫描用的粒子发射器：`LocalCoords = false` ⇒ **已发射的粒子留在原地**，发射器向右走就自然形成
    /// "右侧（发射器处）最密、越往左越疏"；粒子本身往左飘 ⇒ ② 的"逐渐向左移动"。
    /// </summary>
    private static Node2D MakeDealEmitter()
    {
        var material = new ParticleProcessMaterial
        {
            Direction = new Vector3(-1f, 0f, 0f),
            Spread = 14f,
            InitialVelocityMin = DealParticleSpeedMin,
            InitialVelocityMax = DealParticleSpeedMax,
            Gravity = Vector3.Zero,
            DampingMin = DealParticleDampingMin,
            DampingMax = DealParticleDampingMax,
            ScaleMin = 0.45f,
            ScaleMax = 0.95f,
            AngularVelocityMin = -90f,
            AngularVelocityMax = 90f,
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(SlotWidth * 0.5f, SlotHeight * 0.45f, 0f),
            ColorRamp = ParticleFx.Ramp(AeonglassUiPalette.DealAmberHot, AeonglassUiPalette.DealAmberCold),
        };

        var particles = new GpuParticles2D
        {
            Name = "DealSweep",
            Amount = DealParticleAmount,
            Lifetime = DealParticleLife,
            LocalCoords = false,
            Emitting = true,
            Texture = ParticleFx.SoftDot(),
            ProcessMaterial = material,
            ZIndex = 40,
        };

        // "速度连续"三件套：FixedFps = 0 + Interpolate + Explosiveness = 0（见 ParticleFx 注释）。
        ParticleFx.Continuous(particles);
        return particles;
    }
}
