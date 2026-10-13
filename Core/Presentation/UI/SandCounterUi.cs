using Godot;

using MegaCrit.Sts2.Core.Assets;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Systems.SandResource;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// 沙砾的战斗计数器（挂在 <c>scenes/ui/secondary_resource/aeonglass_sand_counter.tscn</c> 上）。
/// </summary>
/// <remarks>
/// <para>
/// 用"我们自己的场景 + 框架标准组件"的方式：根节点就是 RitsuLib 的
/// <see cref="NSecondaryResourceCounterRow" />（行的布局、<b>数字</b>、悬停、点击、增量动画都由框架负责），
/// 我们只在这个场景里定<b>位置与尺寸</b>，并在 <c>_Ready</c> 里套上自己的样式。
/// </para>
/// <para>
/// <b>5 层沙漏艺术是"注入"进去的</b>：框架的图标槽是它内部的
/// <see cref="NSecondaryResourceIcon" />（里面那张贴图来自资源定义，现在是透明的空槽）。
/// 本类把 <see cref="SandCounterArtUi" /> 挂成它的子节点、并把框架那张贴图藏起来 ——
/// 于是<b>位置/尺寸/悬浮/点击全部沿用框架</b>，只有画面换成我们的。
/// <b>不能用 <c>Modulate</c> 去藏框架图标</b>：modulate 会被子节点继承，会把我们的图一起染。
/// </para>
/// <para>
/// 注册见 <see cref="AeonglassSecondaryResources.Register" />：框架通过 NodeAttachment 把它挂到
/// <c>NCombatUi.EnergyCounterContainer</c> 上，并（每帧 / 资源变化时）调
/// <c>Bind(player, visibleDefinitions, autoRefresh: true)</c> 灌数据 —— 那里会调 <see cref="PushLevel" />。
/// </para>
/// <para>
/// <b>倒置的反馈走 <c>SandInvertCmd</c></b>：成功 → <see cref="NotifyInvertSucceeded" /> 播 180° 翻转；
/// 被拒（能量不足）→ <see cref="NotifyInvertRejected" /> 让宝珠红 1 秒。倒置<b>唯一入口</b>仍是那个命令。
/// </para>
/// </remarks>
public partial class SandCounterUi : NSecondaryResourceCounterRow
{
    /// <summary>
    /// 当前活着的计数器（<c>SandInvertCmd</c> 用它回调翻转 / 红珠）。战斗里只会有一个。
    /// </summary>
    public static SandCounterUi? Current { get; private set; }

    private SandCounterArtUi? _art;
    private int _lastLevel = int.MinValue;
    private int _pendingLevel = -1;
    private bool _warnedMissingIcon;

    public override void _Ready()
    {
        base._Ready();

        Configure(AeonglassSecondaryResources.SandCounterStyle);
        Current = this;

        // 框架的图标子树是在 Bind（第一次 update）里才建好的，所以这里只做一次"尽力而为"的延后尝试。
        CallDeferred(nameof(TryAttachArt));
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    /// <summary>沙砾值（0..6）—— 由框架的 update 回调推进来。</summary>
    public void PushLevel(int level)
    {
        level = Const.Paths.Ui.SandCounter.ClampLevel(level);

        TryAttachArt();
        if (_art is null)
        {
            _pendingLevel = level;
            return;
        }

        // 只在"真的变了"时闪白（首次绑定不算变化）。
        if (_lastLevel != int.MinValue && level != _lastLevel)
        {
            _art.FlashGlowOnce();
        }

        _lastLevel = level;
        _art.SetLevel(level);
    }

    /// <summary>倒置成功 → 播 180° 翻转（<paramref name="newLevel" /> = 交换后的新沙砾值）。</summary>
    public void NotifyInvertSucceeded(int newLevel)
    {
        TryAttachArt();
        _art?.PlayInvertFlip(newLevel);
    }

    /// <summary>倒置被拒（如能量不足）→ 宝珠与光晕变红 1 秒。</summary>
    public void NotifyInvertRejected()
    {
        TryAttachArt();
        _art?.FlashOrbRed();
    }

    /// <summary>把 5 层艺术挂进框架的图标槽（幂等；找不到图标就只警告一次、不影响计数器本体）。</summary>
    private void TryAttachArt()
    {
        if (_art is not null)
        {
            return;
        }

        var icon = FindIcon(this);
        if (icon is null)
        {
            if (!_warnedMissingIcon)
            {
                _warnedMissingIcon = true;
                Const.Logger.Warn(
                    $"[{Const.ModId}] [M1] 找不到框架的次级资源图标槽，5 层沙漏艺术没挂上（计数器数字仍可用）。");
            }

            return;
        }

        // 让出图标槽：藏掉框架那张（空槽）贴图。
        // ⚠️ 只动贴图节点，不动 icon 自己的 Modulate —— modulate 会被子节点继承。
        foreach (var child in icon.GetChildren())
        {
            if (child is TextureRect texture)
            {
                texture.Texture = null;
                texture.Visible = false;
            }
        }

        var art = PreloadManager.Cache
            .GetScene(Const.Paths.Ui.SandCounterArtScene)
            .Instantiate<SandCounterArtUi>(PackedScene.GenEditState.Disabled);

        art.Position = Vector2.Zero;
        icon.AddChild(art);
        _art = art;

        if (_pendingLevel >= 0)
        {
            art.SetLevel(_pendingLevel);
            _lastLevel = _pendingLevel;
            _pendingLevel = -1;
        }
    }

    /// <summary>在子树里找框架的图标节点（它不在固定层级上，所以按类型递归找）。</summary>
    private static NSecondaryResourceIcon? FindIcon(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is NSecondaryResourceIcon icon)
            {
                return icon;
            }

            if (FindIcon(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
