using Godot;

using MegaCrit.Sts2.Core.Assets;

using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Systems.Sand;

using STS2_Aeonglass.Core.Presentation.Support;

namespace STS2_Aeonglass.Core.Presentation.SecondaryResource;
/// <summary>沙砾的<b>战斗计数器</b>（挂在 <c>scenes/ui/secondary_resource/aeonglass_sand_counter.tscn</c> 上）。</summary>
/// <remarks>
/// ① 职责：读数显示 + 倒置成功/失败的提示动画（<c>Notify*</c>）。
/// ② 调用时机：RitsuLib 建次级资源计数器时实例化；<c>SandInvertCmd</c> 调通知。
/// ③ 依赖：样式 <c>SandResourceRegistry.SandCounterStyle</c> + 沙漏艺术节点（<c>NodeSearch</c> 找）。
/// ④ ⚠️ <c>Current</c> 是静态单例：战斗结束 / 换场景要清，否则会指向已释放节点。
/// </remarks>

public partial class AeonglassSandCounterUi : NSecondaryResourceCounterRow
{
    /// <summary>
    /// 当前活着的计数器（<c>SandInvertCmd</c> 用它回调翻转 / 红珠）。战斗里只会有一个。
    /// </summary>
    public static AeonglassSandCounterUi? Current { get; private set; }

    private AeonglassSandCounterArt? _art;
    private int _lastLevel = int.MinValue;
    private int _pendingLevel = -1;
    private bool _warnedMissingIcon;

    public override void _Ready()
    {
        base._Ready();

        Configure(SandResourceRegistry.SandCounterStyle);
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

        var icon = NodeSearch.FindDescendant<NSecondaryResourceIcon>(this);
        if (icon is null)
        {
            if (!_warnedMissingIcon)
            {
                _warnedMissingIcon = true;
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Sand} 找不到框架的次级资源图标槽，5 层沙漏艺术没挂上（计数器数字仍可用）。");
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
            .Instantiate<AeonglassSandCounterArt>(PackedScene.GenEditState.Disabled);

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

}
