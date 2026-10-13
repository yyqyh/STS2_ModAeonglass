using Godot;

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2_Aeonglass.Core.Presentation.Visuals;

namespace STS2_Aeonglass.Core.Presentation.UI.ControlPanel;

/// <summary>统一控制面板里的<b>一页</b>：一个机制的状态行 + 按钮区。</summary>
/// <remarks>
/// <para>
/// 2026-10-13 由原来 5 个独立面板（M1 / M2 / M3+M5 / M4 / M6 各一份 <c>PanelContainer</c>）合并而来。
/// 面板外壳（定位、刷新节拍、结果行、找玩家）全部收在 <see cref="AeonglassControlPanel" />，
/// 每页<b>只负责</b>三件事：<see cref="Title" />（页签名）、<see cref="Status" />（状态行）、
/// <see cref="BuildBody" />（按钮与说明）。
/// </para>
/// <para>
/// 纪律：这一层<b>只接按钮</b>，不含任何机制逻辑 —— 按钮一律调机制模块的公开入口
/// （<c>TimeGap.Activate</c> / <c>RewindModule.TryRequest</c> / <c>EnemyTimeModule.*</c> / <c>SandInvertCmd.TryInvert</c> …），
/// 所以正式入口（卡牌 / 遗物 / 按键）接上之后，这些页可以整页退场而机制不受影响。
/// </para>
/// </remarks>
internal abstract partial class ControlPage : VBoxContainer
{
    private static readonly Color Dim = new(1f, 1f, 1f, 0.65f);
    private static readonly Color Ok = new(0.55f, 0.95f, 0.6f);
    private static readonly Color Bad = new(1f, 0.55f, 0.5f);

    private VBoxContainer _info = null!;
    private Label _status = null!;
    private Label _result = null!;
    private VBoxContainer _body = null!;

    /// <summary>所属面板（由面板在挂载前塞进来）。</summary>
    internal AeonglassControlPanel Panel { get; set; } = null!;

    /// <summary>页签名（面板拿它当标签文本）。</summary>
    internal abstract string Title { get; }

    /// <summary>这一页现在能不能用；为假 ⇒ 面板<b>根本不建</b>这一页。</summary>
    internal virtual bool IsAvailable => true;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 4);

        _status = MakeLabel("-", 11, Dim);
        AddChild(_status);

        _info = new VBoxContainer { Visible = false };
        AddChild(_info);

        _result = MakeLabel("-", 11, Colors.White);
        AddChild(_result);

        // ⚠️ 不用 ScrollContainer（会多一层可能吃掉 / 挡住的嵌套）：
        //   页面直接是一个 VBox，按钮就在里面。2026-10-13 用户实测"面板按钮点不动"，先去这层。
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(_body);

        BuildBody(_body);
        Refresh();
    }

    // ============================ 页面实现 ============================

    /// <summary>建按钮 / 说明（往 <paramref name="body" /> 里塞）。</summary>
    protected abstract void BuildBody(VBoxContainer body);

    /// <summary>状态行文本（面板每 0.25s 重算一次）。</summary>
    protected virtual string Status() => "-";

    /// <summary>由面板按节拍调用。</summary>
    internal void Refresh() => _status.Text = Status();

    // ============================ 小工具 ============================

    /// <summary>小节标题。</summary>
    protected void Section(string title) => _body.AddChild(MakeLabel($"— {title} —", 11, Dim));

    /// <summary>一行说明文字。</summary>
    protected void Text(string text) => _body.AddChild(MakeLabel(text, 10, Dim));

    /// <summary>按钮网格。</summary>
    protected GridContainer Grid(int columns)
    {
        var grid = new GridContainer { Columns = columns };
        _body.AddChild(grid);
        return grid;
    }

    /// <summary>加一条常驻信息行（状态行下面、按钮上面的只读区）。</summary>
    protected Label Info(int size = 11)
    {
        _info.Visible = true;
        var label = MakeLabel("-", size, Dim);
        _info.AddChild(label);
        return label;
    }

    /// <summary>把最近一次操作的结果写到结果行（绿=成功 / 红=被拒）。</summary>
    protected void Show(string text, bool bad = false)
    {
        _result.Text = text;
        _result.AddThemeColorOverride("font_color", bad ? Bad : Ok);
    }

    /// <summary>
    /// 按下一个按钮时的统一留痕。
    /// </summary>
    /// <remarks>
    /// 2026-10-13 加：实测里"点了没反应"分不清是**输入没到**还是**机制拒绝了** —— 这一行日志先落地，
    /// 只要 log 里有"面板：按下 X"就说明点击链路是通的，问题在机制侧。
    /// </remarks>
    protected void LogPress(string what)
        => Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Gap} 面板：按下 {what}");

    /// <summary>找我们的玩家：按战斗外观类型认（全部页面共用同一套认法）。</summary>
    protected static Player? Player()
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

    protected static Button Btn(string text, Action onPressed)
    {
        var button = new Button { Text = text, Flat = true };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.Pressed += onPressed;
        return button;
    }

    protected static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }
}
