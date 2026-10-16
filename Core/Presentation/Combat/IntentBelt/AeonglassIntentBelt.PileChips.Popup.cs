using Godot;

using MegaCrit.Sts2.Core.Helpers;

using STS2_Aeonglass.Core.Presentation.Support;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;

/// <summary>抽 / 弃小卡的<b>预览面板</b>（点开看这一堆里有什么；<c>AeonglassIntentBelt</c> 的 partial）。</summary>
/// <remarks>
/// ① 职责：<c>ShowPilePopup</c> / <c>ClosePilePopup</c>。
/// ② 调用时机：点小卡（<c>WirePileChip</c>）、刷新时面板开着就重算。
/// ③ 依赖：牌堆小卡行 <c>_pileRow</c> 的坐标。
/// ④ ⚠️ 面板用后即焚（每次重建），但**小卡本身是池化的**（见主文件的 <c>PileChipSlot</c>）。
/// </remarks>

public partial class AeonglassIntentBelt
{

    private void ClosePilePopup()
    {
        if (_pilePopup is { } existing)
        {
            if (GodotObject.IsInstanceValid(existing))
            {
                existing.QueueFreeSafely();
            }

            _pilePopup = null;
        }
    }

    /// <summary>
    /// 小卡挂进 `_pileRow`、格子排版出尺寸之后，做"缩放 + 实测居中 + 摆两个标签"（每张小卡只做一次）。
    /// </summary>
    /// <remarks>
    /// 为什么必须延后：建小卡时它还没进树，`GetGlobalTransformWithCanvas()` 量到的是无效变换 ⇒ 居中算错、
    /// 标签跟着飘（实测"牌堆和两对数字位置不对"就是这个）。等 `Size` 有效再做，结果才是真的。
    /// </remarks>
    private void ResolvePendingChips()
    {
        if (_pendingChips.Count == 0)
        {
            return;
        }

        for (var i = _pendingChips.Count - 1; i >= 0; i--)
        {
            var chip = _pendingChips[i];
            if (!GodotObject.IsInstanceValid(chip.Cell) || !GodotObject.IsInstanceValid(chip.Icon))
            {
                _pendingChips.RemoveAt(i);
                continue;
            }

            var cellSize = chip.Cell.Size.X > 1f && chip.Cell.Size.Y > 1f ? chip.Cell.Size : chip.Cell.CustomMinimumSize;
            if (cellSize.X <= 1f || cellSize.Y <= 1f || chip.Icon.Size.X <= 1f || chip.Icon.Size.Y <= 1f)
            {
                continue;   // 还没排版好：下一帧再来
            }

            PlacePileChip(chip, cellSize);
            _pendingChips.RemoveAt(i);
        }
    }

    /// <summary>缩放 + 实测居中（本体按钮自带 PivotOffset，直接 <c>Scale</c> 会被推出去 pivot×(1-s)）+ 摆标签 + 调试描边。</summary>
    private static void PlacePileChip(PileChipSlot chip, Vector2 cellSize)
    {
        var iconRect = UiGeometry.FitAndCenterIn(chip.Icon, chip.Cell, cellSize);
        if (PileChipIconNudge != Vector2.Zero)
        {
            // 人工微调：图标挪一格，标签照挪后的矩形摆（"看到的图标 = 能点的格子"只差这一点）。
            chip.Icon.Position += PileChipIconNudge;
            iconRect.Position += PileChipIconNudge;
        }

        if (chip.Count is { } count)
        {
            // 数量：贴图标右下角（内收 4/2 = 微调旋钮，越大越往右下）。
            count.Position = iconRect.Position + iconRect.Size - new Vector2(4f, 2f);
        }

        if (chip.Wither is { } wither)
        {
            // 凋萎 N：贴图标左上角外侧（-6/-14 = 微调旋钮）。
            wither.Position = iconRect.Position + new Vector2(-6f, -14f);
        }

        if (!LoggedPileChipGeometry)
        {
            LoggedPileChipGeometry = true;
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 牌堆小卡：格子={cellSize} 图标=({iconRect.Position}+{iconRect.Size})"
                + $" 缩放={chip.Icon.Scale}｜数量={chip.Count?.Position}｜凋萎={chip.Wither?.Position}");
        }
    }

    /// <summary>牌堆预览：在牌堆行下面弹一个小面板，列出这一堆里有什么（同堆再点关、换堆换内容）。</summary>
    private void ShowPilePopup(string title, IReadOnlyList<IntentCard> pile, Control anchor)
    {
        ClosePilePopup();

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.88f),
            BorderColor = new Color(1f, 1f, 1f, 0.35f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        });

        var box = new VBoxContainer();
        panel.AddChild(box);
        box.AddChild(UiFactory.MakeLabel($"{title}牌堆（{pile.Count}）", 11, Colors.White));
        foreach (var card in pile.Take(24))
        {
            box.AddChild(UiFactory.MakeLabel($"· {card.Describe()}", 10, new Color(1f, 1f, 1f, 0.82f)));
        }

        if (pile.Count == 0)
        {
            box.AddChild(UiFactory.MakeLabel("（空）", 10, new Color(1f, 1f, 1f, 0.6f)));
        }

        AddChild(panel);
        panel.Position = anchor.Position + new Vector2(0f, anchor.Size.Y + 2f) + _pileRow.Position;
        panel.ZIndex = 200;
        panel.MouseFilter = MouseFilterEnum.Stop;
        _pilePopup = panel;

        // 鼠标从面板上移开也关（面板要收鼠标事件，否则收不到 Exited）。
        panel.MouseExited += () =>
        {
            var mouse = panel.GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            if (!panel.GetGlobalRect().HasPoint(mouse))
            {
                ClosePilePopup();
                _popupIsDrawPile = null;
            }
        };
    }
}

