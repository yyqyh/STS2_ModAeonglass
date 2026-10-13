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
using STS2_Aeonglass.Core.Presentation.Visuals;
using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>M2 的战斗按键（<b>一个</b>按钮）：挂在<b>能量计数器正下方</b>，按当前可做的动作显示「坍缩」或「唤回」。</summary>
/// <remarks>
/// <para>
/// <b>为什么是一个按钮</b>（用户 2026-10-13）：坍缩与唤回**必须交替**、而且共用"每回合 1 次"的额度
/// ⇒ 任何时刻最多只有一个合法。所以用一个按钮、文案跟着合法动作走，比并排两个更贴机制。
/// </para>
/// <para>
/// <b>全部用原版资源</b>：控件是原版 <see cref="NButton" />（悬停 / 按下 / 禁用 / 点击音效都是原版那套）、
/// 底板是原版贴图、文字用原版字体。
/// </para>
/// <para>
/// ⚠️ <b>待办（用户 2026-10-13 记录）</b>：① 补按钮**图标**；② 加**按下特效**；③ 加**"可以按下"的特效**
/// （即"可用态"的表现，现在只有原版禁用/可用两档）。见《控制面板与触发按键方案》§6。
/// </para>
/// </remarks>
internal static class GapActionButtons
{
    /// <summary>按钮文字（用户指定）。</summary>
    internal const string CollapseText = "坍缩";

    /// <summary>「坍缩」的悬停说明（用户指定）。</summary>
    internal const string CollapseTip = "坍缩：将手牌置于时空裂隙";

    /// <summary>按钮文字（用户指定）。</summary>
    internal const string RecallText = "唤回";

    /// <summary>「唤回」的悬停说明（用户指定）。</summary>
    internal const string RecallTip = "唤回：用时空裂隙中的牌替换手牌";

    /// <summary>
    /// 相对能量计数器底边的垂直偏移（px，负值 = 往上）。
    /// </summary>
    /// <remarks>
    /// 用户 2026-10-13 第一次口径"正下方"，第二次"再往上 40px" ⇒ 10 - 40 = -30（那时按钮高 38）。
    /// <b>2026-10-13 换成月牙后按钮高 128</b>，为了让"按钮中心"还停在原来那个位置，再减掉半个高度差
    /// （(128-38)/2 = 45）⇒ -75。想再挪只改这一个数。
    /// </remarks>
    internal const float GapBelowCounter = -90f;

    /// <summary>相对能量计数器左缘的水平偏移（px）。</summary>
    internal const float XOffset = 30f;

    /// <summary>
    /// 按钮尺寸。<b>2026-10-13 换成月牙（128×128）</b>：原版底板那条 132×38 的横条不要了。
    /// </summary>
    internal static readonly Vector2 ButtonSize = new(128f, 128f);

    /// <summary>
    /// 文字在月牙"肚子"里的落点（128 画布坐标）。
    /// </summary>
    /// <remarks>
    /// 月牙几何 = 外圆 A 挖掉切圆 B，两圆交点是小角；<b>圆 A 圆心落在圆 B 内部</b>，
    /// 所以实心臂在<b>右下</b>（美术侧已整体左右翻转定死这个方向）。
    /// 圆 A 圆心 = (64,67)（256 画布 (128,134) 的一半），实心臂中点约在它沿"背向 B"方向 20px 处 ⇒ 这里取 (78,86)。
    /// 觉得偏了就只改这个常数。
    /// </remarks>
    internal static readonly Vector2 TextCenter = new(78f, 86f);

    /// <summary>文字所占矩形（相对 <see cref="TextCenter" /> 居中）。</summary>
    internal static readonly Vector2 TextSize = new(66f, 30f);
}

/// <summary>把 M2 战斗按键挂进战斗房间（随房间销毁）。</summary>
internal static class GapActionButtonsHost
{
    internal static void Attach(Node parent)
    {
        AeonglassSettingStore.Initialize();

        if (!AeonglassSettingStore.Current.GapEnabled || !AeonglassSettingStore.Current.GapActionButtons)
        {
            return;
        }

        // ⚠️ 层级历史（别再来回改）：
        //   2026-10-13 曾把按键塞进 EnergyCounterContainer 并排到能量球前面（"画在它下面"），
        //   同时把能量计数器抬到 z=2 —— 结果两者都跑到了**暂停界面压暗层之上**、在暂停界面里
        //   显得"高亮"。用户随即要求「能量计数器和按键恢复到原来高度」⇒ 全部还原：
        //   按键回挂到战斗房间、z 回到 213；能量计数器的 z 一个也不碰。
        foreach (var child in parent.GetChildren())
        {
            if (child is GapActionRow)
            {
                return;
            }
        }

        var row = new GapActionRow
        {
            Name = GapActionRow.NodeName,
            UniqueNameInOwner = true,
            ZIndex = 213,
        };

        parent.AddChild(row);
    }
}

/// <summary>按钮的容器：自己负责"贴到能量计数器正下方"与"每帧决定显示哪个动作、能不能按"。</summary>
internal sealed partial class GapActionRow : Control
{
    internal const string NodeName = "AeonglassGapActionRow";

    /// <summary>入场动画时长 —— 与本体 <c>NEnergyCounter._animDuration</c> 一致（0.6s）。</summary>
    private const float AnimDuration = 0.6f;

    /// <summary>入场起始偏移 —— 与本体 <c>NEnergyCounter._hidePosition</c> 一致（从左上外侧滑进来）。</summary>
    private static readonly Vector2 HideOffset = new(-480f, 128f);

    private GapActionButton _button = null!;
    private bool _loggedMissing;

    /// <summary>我们自己的入场补演（只在"被挂上时能量球已经到位"时才需要）。</summary>
    private bool _animStarted;
    private float _animElapsed = AnimDuration;

    /// <summary>当前按键是否参与鼠标交互（暂停界面里要"看得见但不亮"）。</summary>
    private bool _interactionEnabled = true;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _button = new GapActionButton { Name = "GapActionButton" };
        AddChild(_button);

        Visible = false;
        // 用 Always：即使哪天树被暂停，我们也继续刷新"该不该显示 / 该在哪"。
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        // 暂停界面：按键**要显示**（用户口径），但**不参与交互** —— 否则鼠标划过会把它点亮。
        // 判据用本体暂停菜单自己设的那个标志（NPauseMenu.OnSubmenuOpened ⇒ RunManager.IsPaused = true）。
        var wantInteraction = RunManager.Instance?.IsPaused != true;
        if (_interactionEnabled != wantInteraction)
        {
            _interactionEnabled = wantInteraction;
            _button.MouseFilter = _interactionEnabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        }

        if (NCombatRoom.Instance?.Ui is not { } ui)
        {
            Visible = false;
            return;
        }

        FollowCounter(ui, (float)delta);

        if (Player() is not { } player)
        {
            Visible = false;
            return;
        }

        // ⚠️ 别忘把行显示出来：`Visible` 在 _Ready 里是 false（等能量计数器就位），
        //    少了这一行就是"按钮整个消失" —— 2026-10-13 就是这么踩的。
        Visible = true;

        RefreshState(player);
    }

    /// <summary>
    /// 贴到能量计数器（原版能量球）正下方，并<b>复用它的生命周期</b>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本体能量计数器的进出场是把<b>自己的 position</b> 从 <c>(-480,128)</c> 补间到 <c>(0,0)</c>
    /// （0.6s，Expo-out 进 / Back-in 出，见 <c>NEnergyCounter.AnimIn/AnimOut</c>）。
    /// </para>
    /// <para>
    /// 所以：<b>能量球在演动画时（它的 position ≠ 0）我们就直接跟着它走</b> —— 退场天生一致；
    /// 而<b>如果我们是在能量球已经到位之后才被挂上</b>（常见：房间后挂），就自己补一次同样的入场，
    /// 否则按钮会"啪"地出现、跟能量球不是一套节奏。
    /// </para>
    /// </remarks>
    private void FollowCounter(NCombatUi ui, float dt)
    {
        if (ui.EnergyCounterContainer is not { } container)
        {
            return;
        }

        if (container.GetChildren().OfType<NEnergyCounter>().FirstOrDefault() is not { } orb)
        {
            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Gap} 找不到能量计数器（NEnergyCounter）⇒ M2 战斗按键暂不显示");
            }

            Visible = false;
            return;
        }

        var offsetBelow = new Vector2(GapActionButtons.XOffset, orb.Size.Y + GapActionButtons.GapBelowCounter);

        // 能量球正在走它自己的进出场动画 ⇒ 跟着它（这就是"生命周期同能量计数器"）。
        if (orb.Position.LengthSquared() > 1f)
        {
            _animElapsed = AnimDuration;
            GlobalPosition = orb.GlobalPosition + offsetBelow;
            return;
        }

        // 它已经就位：第一次贴上去时补演一次同款入场。
        if (!_animStarted)
        {
            _animStarted = true;
            _animElapsed = 0f;
        }

        _animElapsed = Mathf.Min(_animElapsed + dt, AnimDuration);
        var remaining = 1f - EaseOutExpo(_animElapsed / AnimDuration);

        // 基准取"能量计数器容器"而不是能量球自己：球上的 position 会被它的动画改。
        GlobalPosition = container.GlobalPosition + offsetBelow + HideOffset * remaining;
    }

    /// <summary>Expo-out（本体那套入场曲线）。</summary>
    private static float EaseOutExpo(float t)
    {
        t = Mathf.Clamp(t, 0f, 1f);
        return Mathf.IsEqualApprox(t, 1f) ? 1f : 1f - Mathf.Pow(2f, -10f * t);
    }

    /// <summary>
    /// 一个按钮 ⇒ 每帧决定"现在显示哪个动作"。
    /// </summary>
    /// <remarks>
    /// 两个都能做（理论上不会：交替 + 每回合 1 次）时按"交替该轮到谁"；两个都不能做时也按交替显示，但按钮灰掉。
    /// </remarks>
    private void RefreshState(Player player)
    {
        var canCollapse = CanCollapse(player);
        var canRecall = CanRecall(player);
        var recall = canCollapse == canRecall ? NextIsRecall(player) : canRecall;

        _button.SetAction(recall, canCollapse || canRecall);
    }

    /// <summary>交替语境下"接下来该谁"：上一次是坍缩 ⇒ 该唤回；否则该坍缩。</summary>
    private static bool NextIsRecall(Player player)
        => GapTurnLimit.LastAction(player) == GapTurnLimit.GapAction.Collapse;

    /// <summary>「坍缩」能不能做：**复用机制自己的预测**（两道闸 + 沙砾 + 手牌非空）。</summary>
    private static bool CanCollapse(Player player)
        => TimeGap.IsReady && TimeGap.PlanCollapse(player).Count > 0;

    /// <summary>「唤回」能不能做：间隙非空 + 两道闸（代价不足也在里面）。</summary>
    private static bool CanRecall(Player player)
        => TimeGap.IsReady && TimeGap.PlanRecall(player).Count > 0;

    /// <summary>找我们的玩家：按战斗外观类型认（与其它面板共用同一套认法）。</summary>
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
}

/// <summary>那一个 M2 战斗按键：原版 <see cref="NButton" />（底板 / 字体都用原版资源）。</summary>
/// <remarks>
/// <para>
/// <b>2026-10-13 换皮</b>：底板从"原版跳过按钮那条横条"换成<b>月牙艺术</b>
/// （<see cref="Const.Paths.Ui.CollapseButtonArtScene" />，3 层 + 三段表现，见 <see cref="CollapseButtonArtUi" />）。
/// 按钮本体仍是原版 <see cref="NButton" />（悬停 / 按下 / 禁用 / 点击音效都是原版那套）。
/// </para>
/// <para>
/// 月牙的两个常态直接对应这个按钮的两个动作：<b>常态 A = 「坍缩」</b>（灰青内层）、
/// <b>常态 B = 「唤回」</b>（玫红内层）。动作切换的那一刻就是"变化"演出 ——
/// 也就是说：<b>坍缩成功 ⇒ 按钮翻成玫红（裂缝闪白 + 上→下染色），唤回 ⇒ 收回玫红</b>。
/// </para>
/// </remarks>
internal sealed partial class GapActionButton : NButton
{
    /// <summary>原版字体（与那块底板同套）。</summary>
    private const string FontPath = "res://themes/kreon_bold_glyph_space_two.tres";

    private static readonly Color TextColor = new(0.992157f, 0.956863f, 0.890196f);
    private static readonly Color OutlineColor = new(0.121569f, 0.25098f, 0.270588f);

    /// <summary>官方 hover tip 的表 / 条目前缀（条目见 <c>localization/zhs/static_hover_tips.json</c>）。</summary>
    private const string TipTable = "static_hover_tips";

    /// <summary>提示框相对按钮的偏移（按钮在能量计数器下方 ⇒ 往上放）。想挪位置就改这里。</summary>
    private static readonly Vector2 TipOffset = new(0f, -140f);

    private CollapseButtonArtUi _art = null!;
    private Label _label = null!;
    private bool _recall;

    public override void _Ready()
    {
        // ⚠️ 原版 NButton._Ready() 会直接抛（它要求子类走 ConnectSignals）——所以这里**不能**调 base._Ready()。
        ConnectSignals();

        CustomMinimumSize = GapActionButtons.ButtonSize;
        Size = GapActionButtons.ButtonSize;
        PivotOffset = GapActionButtons.ButtonSize * 0.5f;
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

    /// <summary>切换"现在代表哪个动作 / 能不能按"（由 <see cref="GapActionRow" /> 每帧调）。</summary>
    internal void SetAction(bool recall, bool enabled)
    {
        // 动作切换 = 月牙换常态：两个方向都演（进「唤回」= 上→下揭出玫红；回「坍缩」= 下→上退回原色）。
        // 只有"动作真的换了"才演 ⇒ 坍缩被拒（沙砾/名额不够）时不会误播。
        if (recall != _recall)
        {
            _art.SetRose(recall, playChange: true);
        }

        _recall = recall;

        var text = recall ? GapActionButtons.RecallText : GapActionButtons.CollapseText;
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

        var label = _recall ? GapActionButtons.RecallText : GapActionButtons.CollapseText;
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 按键：按下 {label}");
        TaskHelper.RunSafely(RunAsync(_recall, player));
    }

    private static async Task RunAsync(bool recall, Player player)
    {
        var label = recall ? GapActionButtons.RecallText : GapActionButtons.CollapseText;
        try
        {
            var result = recall
                ? await GapSequence.RecallAsync(player)
                : await GapSequence.CollapseAsync(player);

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
    private static CollapseButtonArtUi BuildArt()
    {
        var art = PreloadManager.Cache
            .GetScene(Const.Paths.Ui.CollapseButtonArtScene)
            .Instantiate<CollapseButtonArtUi>(PackedScene.GenEditState.Disabled);

        art.MouseFilter = MouseFilterEnum.Ignore;
        return art;
    }

    /// <summary>原版字体 + 原版按钮的描边配色；位置落在月牙的实心臂里（见 <see cref="GapActionButtons.TextCenter" />）。</summary>
    private static Label BuildLabel()
    {
        var label = new Label
        {
            Name = "Text",
            Text = GapActionButtons.CollapseText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        label.Size = GapActionButtons.TextSize;
        label.Position = GapActionButtons.TextCenter - GapActionButtons.TextSize * 0.5f;
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
