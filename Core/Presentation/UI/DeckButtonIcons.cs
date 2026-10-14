using Godot;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;

using STS2RitsuLib.CardPiles.Nodes;

using STS2_Aeonglass.Core.Systems;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Persistence;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>顶栏两颗「牌组」按钮的<b>图标互换</b>（M6 双卡组）。</summary>
/// <remarks>
/// <para>
/// 提示词那边是在悬浮时按状态算的（<c>DeckTipSwapPatch</c>）；图标不能等到悬浮才换，
/// 所以这里在<b>每次切换成功之后</b>主动刷一次（<c>DoubleDeckService.SwitchAsync</c> 的成功出口），
/// 悬浮时再兜一次底。
/// </para>
/// <para>
/// 口径与提示词一致：<b>本体「卡组」按钮里装的是生效中那副、副卡组按钮里是不生效那副</b>，
/// 所以图标跟着"装的是哪一副"走 ⇒ 切换后两颗图标自然互换。
/// </para>
/// <para>
/// 图标本身：原卡组用 <c>deck_a.png</c>（本体那张牌组图标，我们自己出一份，要改直接换图）、
/// 副卡组用 <c>deck_b.png</c>（反色版）。两颗都是我们的图，都不依赖本体图集。
/// </para>
/// </remarks>
internal static class DeckButtonIcons
{
    /// <summary>原卡组（永时）的图标词根。</summary>
    private const string PrimaryStem = "deck_a";

    /// <summary>副卡组（消逝）的图标词根 —— 反色版。</summary>
    private const string SecondaryStem = Const.Paths.CardPiles.DeckBStem;

    /// <summary>图标节点路径 —— 本体那张和 RitsuLib 的牌堆按钮**都**叫这个（实测一致）。</summary>
    private const string IconNodePath = "Control/Icon";

    /// <summary>按"哪颗按钮现在装着哪一副"刷两颗按钮的图标（幂等：贴图没变就不动）。</summary>
    public static void Refresh(Player player)
    {
        if (!CharacterGate.IsActive || !DecksModule.Enabled)
        {
            return;
        }

        var activeIndex = ActiveDeckProvider.ActiveIndexOf(player);
        var topBar = NRun.Instance?.GlobalUi?.TopBar;

        // 本体「卡组」按钮 = 生效中那副。
        if (topBar?.Deck is { } vanillaDeck)
        {
            SetIcon(vanillaDeck, activeIndex);
        }

        // 副卡组按钮 = 不生效那副（递归按牌堆类型认，不靠节点名 / 顺序）。
        if (DoubleDeckService.DeckBPileType is { } deckBType
            && FindPileButton(topBar, deckBType) is { } modDeckB)
        {
            SetIcon(modDeckB, 1 - activeIndex);
        }
    }

    private static void SetIcon(Node button, int deckIndex)
    {
        if (button.GetNodeOrNull<TextureRect>(IconNodePath) is not { } icon)
        {
            return;
        }

        var path = Const.Paths.Ui.PileIcon(deckIndex == 1 ? SecondaryStem : PrimaryStem);
        if (icon.Texture?.ResourcePath == path)
        {
            return;
        }

        if (ResourceLoader.Load<Texture2D>(path) is { } texture)
        {
            icon.Texture = texture;
        }
    }

    /// <summary>在顶栏子树里按牌堆类型找 RitsuLib 的牌堆按钮。</summary>
    private static NModCardPileButton? FindPileButton(Node? root, PileType type)
    {
        if (root is null)
        {
            return null;
        }

        foreach (var child in root.GetChildren())
        {
            if (child is NModCardPileButton { Definition: { } definition } button
                && definition.PileType == type)
            {
                return button;
            }

            if (FindPileButton(child, type) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
