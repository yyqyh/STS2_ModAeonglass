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

using STS2_Aeonglass.Core.Systems.Gap;
namespace STS2_Aeonglass.Core.Presentation.Combat.Gap;
/// <summary>间隙的<b>一个</b>按键行：按"当前合法动作"切换文案与悬停说明。</summary>
/// <remarks>
/// ① 职责：判定当前该显示「坍缩」还是「唤回」，建按钮并同步文字 / tips。
/// ② 调用时机：战斗房间挂载时建，之后每次刷新重算。
/// ③ 依赖：<c>AeonglassGapActionTexts</c>（文案 / 位置）+ 间隙牌数（决定合法动作）。
/// ④ ⚠️ 坍缩/唤回<b>必须交替</b> ⇒ 同时最多一个合法；"每回合 1 次"的额度已取消。
/// </remarks>

internal sealed partial class AeonglassGapActionRow : Control
{
    internal const string NodeName = "AeonglassGapActionRow";

    /// <summary>入场动画时长 —— 与本体 <c>NEnergyCounter._animDuration</c> 一致（0.6s）。</summary>
    private const float AnimDuration = 0.6f;

    /// <summary>入场起始偏移 —— 与本体 <c>NEnergyCounter._hidePosition</c> 一致（从左上外侧滑进来）。</summary>
    private static readonly Vector2 HideOffset = new(-480f, 128f);

    private AeonglassGapActionButton _button = null!;
    private bool _loggedMissing;

    /// <summary>我们自己的入场补演（只在"被挂上时能量球已经到位"时才需要）。</summary>
    private bool _animStarted;
    private float _animElapsed = AnimDuration;

    /// <summary>当前按键是否参与鼠标交互（暂停界面里要"看得见但不亮"）。</summary>
    private bool _interactionEnabled = true;

    /// <summary>还没做过第一次状态刷新（挂上来的第一次是"落到正确常态"，不该播"动作切换"的演出）。</summary>
    private bool _firstRefresh = true;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _button = new AeonglassGapActionButton { Name = "AeonglassGapActionButton" };
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
                    $"[{Const.ModId}] {Const.Prefix.Gap} 找不到能量计数器（NEnergyCounter）⇒ 时空裂隙 战斗按键暂不显示");
            }

            Visible = false;
            return;
        }

        var offsetBelow = new Vector2(AeonglassGapActionTexts.XOffset, orb.Size.Y + AeonglassGapActionTexts.GapBelowCounter);

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
    /// 一个按钮 ⇒ 每帧决定"现在显示哪个动作、能不能按"。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>显示哪个动作：跟着「间隙」里有没有牌走</b>（用户 2026-10-14 口径）——
    /// 有牌 ⇒ 「唤回」（把间隙里的牌换回手上）；没牌 ⇒ 「坍缩」（把手牌存进间隙）。
    /// </para>
    /// <para>
    /// 旧实现是"两个都能做时按交替顺序猜该轮到谁"，而交替记录是**按战斗**记的、每场战斗重新开始
    /// ⇒ 战斗一开始（间隙里可能还留着上一场存的牌）它会显示「坍缩」，与"间隙有牌"直接矛盾。
    /// </para>
    /// <para>
    /// "必须交替 / 沙砾够不够"现在只决定它<b>能不能按</b>（灰掉），不再参与"显示哪个动作"。
    /// </para>
    /// </remarks>
    private void RefreshState(Player player)
    {
        if (!GapStore.IsReady)
        {
            _button.SetAction(recall: false, enabled: false);
            return;
        }

        var recall = GapStore.CountOf(player) > 0;
        var enabled = recall ? CanRecall(player) : CanCollapse(player);
        // 第一次只落位、不演"切换"：刚挂上来就放一次玫红擦除会看着像"刚刚坍缩过"。
        _button.SetAction(recall, enabled, playChange: !_firstRefresh);
        _firstRefresh = false;
    }

    /// <summary>「坍缩」能不能做：**复用机制自己的预测**（两道闸 + 沙砾 + 手牌非空）。</summary>
    private static bool CanCollapse(Player player)
        => GapStore.IsReady && GapCollapseActions.PlanCollapse(player).Count > 0;

    /// <summary>「唤回」能不能做：间隙非空 + 两道闸（代价不足也在里面）。</summary>
    private static bool CanRecall(Player player)
        => GapStore.IsReady && GapRecallPlan.PlanRecall(player).Count > 0;

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
