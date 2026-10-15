using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Presentation.Character;
using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Presentation.Combat.Gap;
/// <summary>时空裂隙 按键本体（原版 <c>NButton</c> 派生：悬停 / 按下 / 禁用 / 点击音效都是原版那套）。</summary>
/// <remarks>
/// ① 职责：一颗按钮 + 月牙艺术 + 文字 Label（位置/尺寸取 <c>AeonglassGapActionTexts</c>）。
/// ② 调用时机：<c>AeonglassGapActionRow</c> 构建时。
/// ③ 依赖：原版按钮控件 + 月牙艺术 + 原版字体。
/// ④ ⚠️ 待办（用户 2026-10-13）：补按钮图标 / 按下特效 / "可用态"特效 —— 见 <c>Presentation/README.md</c>。
/// </remarks>

internal sealed partial class AeonglassGapActionButton : NButton
{
    /// <summary>原版字体（与那块底板同套）。</summary>
    private const string FontPath = "res://themes/kreon_bold_glyph_space_two.tres";

    private static readonly Color TextColor = new(0.992157f, 0.956863f, 0.890196f);
    private static readonly Color OutlineColor = new(0.121569f, 0.25098f, 0.270588f);

    /// <summary>官方 hover tip 的表 / 条目前缀（条目见 <c>localization/zhs/static_hover_tips.json</c>）。</summary>
    private const string TipTable = "static_hover_tips";

    /// <summary>提示框相对按钮的偏移（按钮在能量计数器下方 ⇒ 往上放）。想挪位置就改这里。</summary>
    private static readonly Vector2 TipOffset = new(0f, -140f);

    private AeonglassGapActionArt _art = null!;
    private Label _label = null!;
    private bool _recall;

    public override void _Ready()
    {
        // ⚠️ 原版 NButton._Ready() 会直接抛（它要求子类走 ConnectSignals）——所以这里**不能**调 base._Ready()。
        ConnectSignals();

        CustomMinimumSize = AeonglassGapActionTexts.ButtonSize;
        Size = AeonglassGapActionTexts.ButtonSize;
        PivotOffset = AeonglassGapActionTexts.ButtonSize * 0.5f;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;

        _art = BuildArt();
        AddChild(_art);
        _label = BuildLabel();
        AddChild(_label);

        // 悬停说明走**官方**那套 hover tip（与卡牌 / 遗物 / 能量球同一个框）：进出各一次。
        MouseEntered += OnHovered;
        MouseExited += OnUnhovered;

        SetAction(recall: false, enabled: false);
    }

    /// <summary>切换"现在代表哪个动作 / 能不能按"（由 <see cref="AeonglassGapActionRow" /> 每帧调）。</summary>
    /// <param name="recall">true = 显示「唤回」，false = 显示「坍缩」。</param>
    /// <param name="enabled">能不能按（灰色 = 不能）。</param>
    /// <param name="playChange">动作真的换了时要不要演"变化"（首次落位传 false）。</param>
    internal void SetAction(bool recall, bool enabled, bool playChange = true)
    {
        // 动作切换 = 月牙换常态：两个方向都演（进「唤回」= 上→下揭出玫红；回「坍缩」= 下→上退回原色）。
        // 只有"动作真的换了"才演 ⇒ 坍缩被拒（沙砾/名额不够）时不会误播。
        if (recall != _recall)
        {
            _art.SetRose(recall, playChange);
        }

        _recall = recall;

        var text = recall ? AeonglassGapActionTexts.RecallText : AeonglassGapActionTexts.CollapseText;
        if (_label.Text != text)
        {
            _label.Text = text;
        }

        if (enabled != IsEnabled)
        {
            if (enabled)
            {
                Enable();
            }
            else
            {
                Disable();
            }
        }
    }

    /// <summary>悬停：弹官方提示框（文案按"此刻代表哪个动作"取，所以进/出之间切动作也没问题）。</summary>
    private void OnHovered()
    {
        var key = _recall ? "STS2_AEONGLASS_GAP_RECALL" : "STS2_AEONGLASS_GAP_COLLAPSE";
        var tip = new HoverTip(
            new LocString(TipTable, key + ".title"),
            new LocString(TipTable, key + ".description"));

        // 照本体 NStarCounter 的接法：CreateAndShow 后自己摆位置，离开时 Remove。
        NHoverTipSet.CreateAndShow(this, tip)?.SetGlobalPosition(GlobalPosition + TipOffset);
    }

    /// <summary>离开：收掉提示框。</summary>
    private void OnUnhovered() => NHoverTipSet.Remove(this);

    /// <summary>松手才算点击（原版 NClickableControl 的语义）。</summary>
    protected override void OnRelease()
    {
        if (Player() is not { } player)
        {
            return;
        }

        var label = _recall ? AeonglassGapActionTexts.RecallText : AeonglassGapActionTexts.CollapseText;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 按键：按下 {label}");
        TaskHelper.RunSafely(RunAsync(_recall, player));
    }

    private static async Task RunAsync(bool recall, Player player)
    {
        var label = recall ? AeonglassGapActionTexts.RecallText : AeonglassGapActionTexts.CollapseText;
        try
        {
            var result = recall
                ? await AeonglassGapSequence.RecallAsync(player)
                : await AeonglassGapSequence.CollapseAsync(player);

            Const.Logger.Info(
                result.Ok
                    ? $"[{Const.ModId}] {Const.Prefix.Gap} 按键 {label}：{result.Message}"
                    : $"[{Const.ModId}] {Const.Prefix.Gap} 按键 {label} 被拒：{result.Message}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Gap} 按键 {label} 抛异常：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>月牙艺术（3 层：外金边 / 内月牙 / 裂缝，自带摇摆与闪色）。</summary>
    private static AeonglassGapActionArt BuildArt()
    {
        var art = PreloadManager.Cache
            .GetScene(Const.Paths.Ui.CollapseButtonArtScene)
            .Instantiate<AeonglassGapActionArt>(PackedScene.GenEditState.Disabled);

        art.MouseFilter = MouseFilterEnum.Ignore;
        return art;
    }

    /// <summary>原版字体 + 原版按钮的描边配色；位置落在月牙的实心臂里（见 <see cref="AeonglassGapActionTexts.TextCenter" />）。</summary>
    private static Label BuildLabel()
    {
        var label = new Label
        {
            Name = "Text",
            Text = AeonglassGapActionTexts.CollapseText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        label.Size = AeonglassGapActionTexts.TextSize;
        label.Position = AeonglassGapActionTexts.TextCenter - AeonglassGapActionTexts.TextSize * 0.5f;
        label.AddThemeFontOverride("font", PreloadManager.Cache.GetAsset<Font>(FontPath));
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeColorOverride("font_outline_color", OutlineColor);
        label.AddThemeConstantOverride("outline_size", 8);
        return label;
    }

    /// <summary>找我们的玩家（与面板同一套认法：按战斗外观类型认）。</summary>
    private static Player? Player()
    {
        if (NCombatRoom.Instance is not { } room)
        {
            return null;
        }

        foreach (var node in room.CreatureNodes)
        {
            if (node?.Visuals is AeonglassCreatureVisuals)
            {
                return node.Entity.Player;
            }
        }

        return null;
    }

    // ================== 只让"月牙"接点击 ==================

    /// <summary>圆 A（月牙的外弧）圆心与半径 —— 128 画布：256 画布的 (128,134) / r88 各取一半。</summary>
    private static readonly Vector2 CircleA = new(64f, 67f);

    private const float CircleARadius = 44f;

    /// <summary>切圆 B（被挖掉那块）—— 256 画布 (80,73) / r84 的一半。</summary>
    private static readonly Vector2 CircleB = new(40f, 36.5f);

    private const float CircleBRadius = 42f;

    /// <summary>
    /// <b>把点击区收成"月牙本身"</b>，而不是整个 128×128 的方框。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 月牙 = 圆 A 挖掉切圆 B，实心只在那条"臂"上；框里其余地方是透明的。
    /// 不收的话，这个透明大空框会挡住下面的原版 UI（抽牌堆按钮就压在它下面），
    /// 2026-10-13 用户实测到的"点不了了"就是这个（另一头的原因）。
    /// </para>
    /// <para>
    /// 不做 <c>IsEnabled</c> 判断：禁用时也要能悬停看说明（禁用本身由 <c>NButton</c> 那套处理点击）。
    /// </para>
    /// </remarks>
    public override bool _HasPoint(Vector2 point)
    {
        // 外弧给 2px 宽容（美术边缘有做旧/抗锯齿），内洞也收 2px。
        if (point.DistanceTo(CircleA) > CircleARadius + 2f)
        {
            return false;
        }

        return point.DistanceTo(CircleB) > CircleBRadius - 2f;
    }
}
