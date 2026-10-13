using Godot;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Presentation.UI.ControlPanel;

/// <summary>
/// 战斗内<b>唯一的</b>控制面板：一个外壳 + 按机制分的页签。
/// </summary>
/// <remarks>
/// <para>
/// 2026-10-13 合并：原先 M1 / M2 / M3+M5 / M4 / M6 各挂一个面板、外加一个外观调试面板，
/// 一共 7 个节点、7 个开关、5 份复制粘贴的骨架。现在只有<b>一个</b>节点、<b>一个</b>开关
/// （<c>AeonglassSetting.ControlPanel</c>），页签按机制总开关出现。
/// </para>
/// <para>
/// 位置固定在屏幕左侧 <c>(12, 20)</c>，宽度 470（高度跟着视口，最高 980）；按 <c>F8</c> 收起 / 展开，
/// 展开时每 0.25s 刷一次各页状态行。
/// </para>
/// <para>
/// 面板<b>不含机制逻辑</b>：页签里的按钮只调各机制模块的公开入口（见 <see cref="ControlPage" />）。
/// </para>
/// </remarks>
internal partial class AeonglassControlPanel : PanelContainer
{
    internal const string NodeName = "AeonglassControlPanel";

    /// <summary>
    /// 面板收起 / 展开的快捷键。
    /// </summary>
    /// <remarks>
    /// ⚠️ 这只是"整块面板"的开关；<b>机制动作</b>的正式按键表（倒置 / 坍缩 / 回归 …）见
    /// 《永世沙漏 · 控制面板与触发按键方案》—— 那张表定稿后要挪进设置页做成可改键。
    /// </remarks>
    private static readonly Key Hotkey = Key.F8;

    private const float RefreshInterval = 0.25f;
    private const float PanelWidth = 470f;
    private const float CollapsedHeight = 38f;

    private static readonly Color Dim = new(1f, 1f, 1f, 0.65f);

    private readonly List<ControlPage> _pages = [];
    private readonly List<Button> _tabs = [];

    private HBoxContainer _tabStrip = null!;
    private VBoxContainer _pageHolder = null!;
    private Button _collapse = null!;
    private float _timer;
    private bool _collapsed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.68f),
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        });

        var root = new VBoxContainer();
        AddChild(root);

        var head = new HBoxContainer();
        root.AddChild(head);
        head.AddChild(MakeLabel($"{Const.DisplayName} · 控制面板", 14, Colors.White));
        head.AddChild(MakeLabel($"({Hotkey})", 10, Dim));
        _collapse = MakeButton("收起", ToggleCollapse);
        head.AddChild(_collapse);

        // ⚠️ 2026-10-13 用户实测"面板里的按钮点不动" ⇒ 去掉 TabContainer / ScrollContainer 两层嵌套，
        //    改成"一条自绘页签 + 一个页面容器"（结构与被验证过可点的老面板一致）。
        _tabStrip = new HBoxContainer { Name = "Tabs" };
        root.AddChild(_tabStrip);

        _pageHolder = new VBoxContainer
        {
            Name = "Pages",
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_pageHolder);

        BuildPages();
        SetProcess(true);
    }

    /// <summary>按机制总开关建页签（关掉的机制连页都不建）。</summary>
    private void BuildPages()
    {
        var setting = AeonglassSettingStore.Current;

        AddPage(new SandControlPage(), setting.SandEnabled);
        AddPage(new StasisControlPage(), setting.StasisEnabled);
        AddPage(new GapControlPage(), setting.GapEnabled);
        AddPage(new IntentControlPage(), setting.IntentEnabled);
        AddPage(new RewindControlPage(), setting.RewindEnabled);
        AddPage(new DeckControlPage(), setting.DecksEnabled);
        // 外观页没有"机制总开关"：它是美术调参工具，永远在。
        AddPage(new AppearanceControlPage(), true);
    }

    private void AddPage(ControlPage page, bool enabled)
    {
        if (!enabled || !page.IsAvailable)
        {
            return;
        }

        page.Panel = this;
        page.Name = $"Page{_pages.Count}";
        page.Visible = false;
        _pageHolder.AddChild(page);
        _pages.Add(page);

        var index = _pages.Count - 1;
        var tab = MakeTab(page.Title, () => ShowPage(index));
        _tabStrip.AddChild(tab);
        _tabs.Add(tab);

        if (index == 0)
        {
            ShowPage(0);
        }
    }

    /// <summary>切页（只有一个页面可见 —— 这就是"页签"的全部实现）。</summary>
    private void ShowPage(int index)
    {
        for (var i = 0; i < _pages.Count; i++)
        {
            _pages[i].Visible = i == index;
            _tabs[i].AddThemeColorOverride("font_color", i == index ? Colors.White : new Color(1f, 1f, 1f, 0.6f));
        }
    }

    public override void _Process(double delta)
    {
        FollowViewport();

        if (_collapsed)
        {
            return;
        }

        _timer += (float)delta;
        if (_timer < RefreshInterval)
        {
            return;
        }

        _timer = 0f;
        foreach (var page in _pages)
        {
            page.Refresh();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key && key.Keycode == Hotkey)
        {
            ToggleCollapse();
            GetViewport()?.SetInputAsHandled();
        }
    }

    private void ToggleCollapse()
    {
        _collapsed = !_collapsed;
        _tabStrip.Visible = !_collapsed;
        _pageHolder.Visible = !_collapsed;
        _collapse.Text = _collapsed ? "展开" : "收起";
    }

    private void FollowViewport()
    {
        if (GetViewport() is not { } viewport)
        {
            return;
        }

        var visible = viewport.GetVisibleRect().Size;
        Size = new Vector2(PanelWidth, _collapsed ? CollapsedHeight : Mathf.Min(visible.Y - 40f, 980f));
        Position = new Vector2(12f, 20f);
    }

    private static Button MakeButton(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        button.AddThemeFontSizeOverride("font_size", 11);
        button.Pressed += onPressed;
        return button;
    }

    private static Button MakeTab(string text, Action onPressed)
    {
        var button = new Button { Text = text, Flat = true };
        button.AddThemeFontSizeOverride("font_size", 12);
        button.Pressed += onPressed;
        return button;
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
